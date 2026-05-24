using System;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Ed25519 signature verification used by the auto-updater. .NET 8 has no
    /// public Ed25519 API yet, so we go through BouncyCastle (already pulled in
    /// transitively by SIPSorcery).
    /// </summary>
    internal static class Ed25519Verify
    {
        public static bool Verify(byte[] publicKeyRaw, byte[] message, byte[] signature)
        {
            // Ed25519 keys are 32 bytes and signatures are 64 bytes — both are fixed by RFC 8032.
            if (publicKeyRaw == null || publicKeyRaw.Length != 32)
                throw new ArgumentException("Ed25519 public key must be exactly 32 bytes", nameof(publicKeyRaw));
            if (signature == null || signature.Length != 64)
                return false;

            var key = new Ed25519PublicKeyParameters(publicKeyRaw, 0);
            var signer = new Ed25519Signer();
            signer.Init(false, key);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.VerifySignature(signature);
        }
    }
}
