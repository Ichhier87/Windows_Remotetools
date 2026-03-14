using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Manages WebRTC-based audio group calls with the CentralServer.
    /// Handles group membership, peer connections, mic capture and audio playback.
    /// </summary>
    public class AudioGroupManager : IDisposable
    {
        private string? _groupId;
        private bool _inGroup;
        private readonly object _groupLock = new object();

        private readonly ConcurrentDictionary<string, RTCPeerConnection> _peers = new();
        private readonly ConcurrentDictionary<string, (BufferedWaveProvider Buffer, WaveOutEvent Player)> _peerPlayback = new();

        private WaveInEvent? _waveIn;
        private readonly object _micLock = new object();

        private readonly Func<JObject, Task> _sendMessage;

        // PCMU: 8 kHz, 16-bit mono
        private static readonly WaveFormat AudioFormat = new WaveFormat(8000, 16, 1);

        public bool InGroup => _inGroup;
        public string? GroupId => _groupId;

        public AudioGroupManager(Func<JObject, Task> sendMessage)
        {
            _sendMessage = sendMessage;
        }

        // ── Group lifecycle ────────────────────────────────────────────────────

        public async Task Join(string groupId)
        {
            lock (_groupLock)
            {
                if (_inGroup) return;
                _inGroup = true;
                _groupId = groupId;
            }

            Console.WriteLine($"[AudioGroup] Joining group: {groupId}");
            StartMicrophone();

            await _sendMessage(new JObject
            {
                ["type"] = "AUDIO_GROUP_JOIN",
                ["group_id"] = groupId
            });
        }

        public async Task Leave()
        {
            string? groupId;
            lock (_groupLock)
            {
                if (!_inGroup) return;
                groupId = _groupId;
                _inGroup = false;
                _groupId = null;
            }

            Console.WriteLine($"[AudioGroup] Leaving group: {groupId}");
            StopMicrophone();
            CloseAllPeers();

            if (groupId != null)
            {
                await _sendMessage(new JObject
                {
                    ["type"] = "AUDIO_GROUP_LEAVE",
                    ["group_id"] = groupId
                });
            }
        }

        public void Kicked()
        {
            Console.WriteLine("[AudioGroup] Kicked from group");
            _ = Leave();
        }

        // ── Peer events from server ────────────────────────────────────────────

        /// <summary>Server sends us the list of peers already in the group — we create an offer for each.</summary>
        public async Task OnPeers(JArray peerIds)
        {
            foreach (var token in peerIds)
                await CreateOfferFor(token.ToString());
        }

        /// <summary>A new peer joined — they will send us an offer.</summary>
        public void OnPeerJoined(string peerId)
        {
            Console.WriteLine($"[AudioGroup] Peer joined: {peerId}");
            // The incoming peer initiates the offer; just ensure the connection slot is ready
            GetOrCreatePeer(peerId);
        }

        public void OnPeerLeft(string peerId)
        {
            Console.WriteLine($"[AudioGroup] Peer left: {peerId}");
            ClosePeer(peerId);
        }

        // ── WebRTC signaling ───────────────────────────────────────────────────

        public async Task OnWebRtcOffer(string fromPeerId, string sdp)
        {
            Console.WriteLine($"[AudioGroup] Offer from {fromPeerId}");
            var pc = GetOrCreatePeer(fromPeerId);

            var result = pc.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.offer,
                sdp = sdp
            });

            if (result != SetDescriptionResultEnum.OK)
            {
                Console.WriteLine($"[AudioGroup] setRemoteDescription failed: {result}");
                return;
            }

            var answer = pc.createAnswer();
            await pc.setLocalDescription(answer);

            await _sendMessage(new JObject
            {
                ["type"] = "WEBRTC_ANSWER",
                ["from"] = fromPeerId,
                ["sdp"] = answer.sdp
            });
        }

        public void OnWebRtcAnswer(string fromPeerId, string sdp)
        {
            Console.WriteLine($"[AudioGroup] Answer from {fromPeerId}");
            if (!_peers.TryGetValue(fromPeerId, out var pc)) return;

            pc.setRemoteDescription(new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp = sdp
            });
        }

        public void OnIceCandidate(string fromPeerId, JObject candidateObj)
        {
            if (!_peers.TryGetValue(fromPeerId, out var pc)) return;

            var sdpMLineRaw = candidateObj["sdpMLineIndex"]?.ToObject<int?>() ?? 0;
            pc.addIceCandidate(new RTCIceCandidateInit
            {
                candidate = candidateObj["candidate"]?.ToString() ?? "",
                sdpMid = candidateObj["sdpMid"]?.ToString(),
                sdpMLineIndex = (ushort)sdpMLineRaw
            });
        }

        // ── Internal WebRTC helpers ────────────────────────────────────────────

        private async Task CreateOfferFor(string peerId)
        {
            Console.WriteLine($"[AudioGroup] Creating offer for {peerId}");
            var pc = GetOrCreatePeer(peerId);

            var offer = pc.createOffer();
            await pc.setLocalDescription(offer);

            await _sendMessage(new JObject
            {
                ["type"] = "WEBRTC_OFFER",
                ["to"] = peerId,
                ["sdp"] = offer.sdp
            });
        }

        private RTCPeerConnection GetOrCreatePeer(string peerId)
            => _peers.GetOrAdd(peerId, id => CreatePeerConnection(id));

        private RTCPeerConnection CreatePeerConnection(string peerId)
        {
            var pc = new RTCPeerConnection(new RTCConfiguration
            {
                iceServers = new List<RTCIceServer>
                {
                    new RTCIceServer { urls = "stun:stun.l.google.com:19302" }
                }
            });

            var audioTrack = new MediaStreamTrack(new[] { SDPWellKnownMediaFormatsEnum.PCMU });
            pc.addTrack(audioTrack);

            pc.onicecandidate += async (candidate) =>
            {
                if (candidate == null) return;
                await _sendMessage(new JObject
                {
                    ["type"] = "WEBRTC_ICE_CANDIDATE",
                    ["to"] = peerId,
                    ["candidate"] = new JObject
                    {
                        ["candidate"] = candidate.candidate,
                        ["sdpMid"] = candidate.sdpMid,
                        ["sdpMLineIndex"] = (int?)candidate.sdpMLineIndex
                    }
                });
            };

            pc.OnRtpPacketReceived += (ep, mediaType, packet) =>
            {
                if (mediaType == SDPMediaTypesEnum.audio)
                    PlayIncomingAudio(peerId, packet.Payload);
            };

            pc.onconnectionstatechange += (state) =>
            {
                Console.WriteLine($"[AudioGroup] Peer {peerId}: {state}");
                if (state == RTCPeerConnectionState.failed ||
                    state == RTCPeerConnectionState.closed)
                    ClosePeer(peerId);
            };

            return pc;
        }

        // ── Audio I/O ─────────────────────────────────────────────────────────

        private void StartMicrophone()
        {
            lock (_micLock)
            {
                if (_waveIn != null) return;
                try
                {
                    _waveIn = new WaveInEvent
                    {
                        WaveFormat = AudioFormat,
                        BufferMilliseconds = 20
                    };
                    _waveIn.DataAvailable += OnMicData;
                    _waveIn.StartRecording();
                    Console.WriteLine("[AudioGroup] Microphone started");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AudioGroup] Mic start failed: {ex.Message}");
                    _waveIn?.Dispose();
                    _waveIn = null;
                }
            }
        }

        private void StopMicrophone()
        {
            lock (_micLock)
            {
                if (_waveIn == null) return;
                try { _waveIn.StopRecording(); } catch { }
                _waveIn.Dispose();
                _waveIn = null;
                Console.WriteLine("[AudioGroup] Microphone stopped");
            }
        }

        private void OnMicData(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            int sampleCount = e.BytesRecorded / 2;
            var encoded = new byte[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(e.Buffer[i * 2] | (e.Buffer[i * 2 + 1] << 8));
                encoded[i] = Pcm16ToMuLaw(s);
            }

            foreach (var (_, pc) in _peers)
            {
                if (pc.connectionState == RTCPeerConnectionState.connected)
                    pc.SendAudio((uint)sampleCount, encoded);
            }
        }

        private void PlayIncomingAudio(string peerId, byte[] pcmuData)
        {
            // Decode PCMU → PCM16
            var pcm = new byte[pcmuData.Length * 2];
            for (int i = 0; i < pcmuData.Length; i++)
            {
                short s = MuLawToPcm16(pcmuData[i]);
                pcm[i * 2] = (byte)(s & 0xFF);
                pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }

            var (buffer, _) = _peerPlayback.GetOrAdd(peerId, _ =>
            {
                var buf = new BufferedWaveProvider(AudioFormat)
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(1)
                };
                var player = new WaveOutEvent();
                player.Init(buf);
                player.Play();
                return (buf, player);
            });

            buffer.AddSamples(pcm, 0, pcm.Length);
        }

        private void ClosePeer(string peerId)
        {
            if (_peers.TryRemove(peerId, out var pc))
            {
                try { pc.Close("done"); pc.Dispose(); } catch { }
            }
            if (_peerPlayback.TryRemove(peerId, out var pb))
            {
                try { pb.Player.Stop(); pb.Player.Dispose(); } catch { }
            }
        }

        private void CloseAllPeers()
        {
            foreach (var id in _peers.Keys.ToArray())
                ClosePeer(id);
        }

        // ── G.711 µ-law codec (ITU-T G.711) ──────────────────────────────────

        private static byte Pcm16ToMuLaw(short pcm)
        {
            const int Bias = 0x84;
            const int Clip = 32635;

            int sign = (pcm >> 8) & 0x80;
            if (sign != 0) pcm = (short)-pcm;
            if (pcm > Clip) pcm = Clip;
            int sample = pcm + Bias;

            int exp = 7;
            for (int mask = 0x4000; (sample & mask) == 0 && exp > 0; exp--, mask >>= 1) { }
            int mantissa = (sample >> (exp + 3)) & 0x0F;
            return (byte)(~(sign | (exp << 4) | mantissa));
        }

        private static short MuLawToPcm16(byte mulaw)
        {
            int x = ~mulaw;
            int sign = x & 0x80;
            int exp = (x >> 4) & 0x07;
            int mantissa = x & 0x0F;
            int sample = ((mantissa << 1) | 1) << (exp + 2);
            sample -= 0x21;
            return (short)(sign != 0 ? -sample : sample);
        }

        public void Dispose()
        {
            _ = Leave();
        }
    }
}
