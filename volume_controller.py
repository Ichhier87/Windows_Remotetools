"""
Volume Controller for Windows using pycaw
"""
import threading
import time
from ctypes import cast, POINTER
from comtypes import CLSCTX_ALL
from pycaw.pycaw import AudioUtilities, IAudioEndpointVolume


class VolumeController:
    """Handles Windows system volume control and enforcement"""

    def __init__(self, config):
        self.config = config
        self.devices = None
        self.interface = None
        self.volume = None
        self._monitoring = False
        self._monitor_thread = None

        self._initialize_audio()

    def _initialize_audio(self):
        """Initialize audio interface"""
        try:
            self.devices = AudioUtilities.GetSpeakers()
            self.interface = self.devices.Activate(
                IAudioEndpointVolume._iid_, CLSCTX_ALL, None)
            self.volume = cast(self.interface, POINTER(IAudioEndpointVolume))
        except Exception as e:
            print(f"Error initializing audio: {e}")
            raise

    def get_volume(self):
        """Get current volume level (0.0 to 1.0)"""
        try:
            return self.volume.GetMasterVolumeLevelScalar()
        except Exception as e:
            print(f"Error getting volume: {e}")
            return 0.0

    def get_volume_percent(self):
        """Get current volume as percentage (0-100)"""
        return int(self.get_volume() * 100)

    def set_volume(self, level):
        """
        Set volume level

        Args:
            level: Volume level (0.0 to 1.0) or (0-100 as int)
        """
        try:
            # Convert percentage to scalar if needed
            if isinstance(level, int) and level > 1:
                level = level / 100.0

            # Clamp to valid range
            level = max(0.0, min(1.0, level))

            # Apply max volume limit if enforced
            if self.config.enforce_max_volume:
                max_level = self.config.max_volume / 100.0
                level = min(level, max_level)

            self.volume.SetMasterVolumeLevelScalar(level, None)
        except Exception as e:
            print(f"Error setting volume: {e}")

    def set_volume_percent(self, percent):
        """Set volume as percentage (0-100)"""
        self.set_volume(percent / 100.0)

    def get_mute(self):
        """Get current mute state"""
        try:
            return bool(self.volume.GetMute())
        except Exception as e:
            print(f"Error getting mute state: {e}")
            return False

    def set_mute(self, mute):
        """Set mute state"""
        try:
            self.volume.SetMute(1 if mute else 0, None)
        except Exception as e:
            print(f"Error setting mute: {e}")

    def toggle_mute(self):
        """Toggle mute state"""
        self.set_mute(not self.get_mute())

    def enforce_max_volume_once(self):
        """Enforce maximum volume limit once"""
        if self.config.enforce_max_volume:
            current = self.get_volume_percent()
            if current > self.config.max_volume:
                self.set_volume_percent(self.config.max_volume)
                print(f"Volume limited: {current}% -> {self.config.max_volume}%")

    def _monitor_volume(self):
        """Background thread to continuously monitor and enforce volume limit"""
        while self._monitoring:
            self.enforce_max_volume_once()
            time.sleep(self.config.volume_check_interval)

    def start_monitoring(self):
        """Start background volume monitoring"""
        if not self._monitoring:
            self._monitoring = True
            self._monitor_thread = threading.Thread(target=self._monitor_volume, daemon=True)
            self._monitor_thread.start()
            print("Volume monitoring started")

    def stop_monitoring(self):
        """Stop background volume monitoring"""
        if self._monitoring:
            self._monitoring = False
            if self._monitor_thread:
                self._monitor_thread.join(timeout=2)
            print("Volume monitoring stopped")

    def set_max_volume_limit(self, max_percent):
        """
        Set maximum volume limit

        Args:
            max_percent: Maximum volume in percent (0-100)
        """
        max_percent = max(0, min(100, max_percent))
        self.config.max_volume = max_percent
        self.config.save_config()
        print(f"Maximum volume set to {max_percent}%")

        # Immediately enforce the new limit
        self.enforce_max_volume_once()

    def enable_enforcement(self, enable=True):
        """Enable or disable volume limit enforcement"""
        self.config.enforce_max_volume = enable
        self.config.save_config()
        print(f"Volume enforcement {'enabled' if enable else 'disabled'}")
