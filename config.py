"""
Configuration file for Windows Remote Tools Tray Application
"""
import os
import json

class Config:
    """Configuration handler for the application"""

    # Default WebSocket server settings
    DEFAULT_WS_HOST = "localhost"
    DEFAULT_WS_PORT = 8765

    # Default volume settings
    DEFAULT_MAX_VOLUME = 100  # Maximum volume in percent (0-100)
    DEFAULT_VOLUME_CHECK_INTERVAL = 1.0  # Seconds between volume checks

    # Application settings
    APP_NAME = "Windows Remote Tools"
    CONFIG_FILE = "config.json"

    def __init__(self):
        self.ws_host = self.DEFAULT_WS_HOST
        self.ws_port = self.DEFAULT_WS_PORT
        self.max_volume = self.DEFAULT_MAX_VOLUME
        self.volume_check_interval = self.DEFAULT_VOLUME_CHECK_INTERVAL
        self.enforce_max_volume = True

        self.load_config()

    def load_config(self):
        """Load configuration from file if it exists"""
        if os.path.exists(self.CONFIG_FILE):
            try:
                with open(self.CONFIG_FILE, 'r') as f:
                    data = json.load(f)
                    self.ws_host = data.get('ws_host', self.DEFAULT_WS_HOST)
                    self.ws_port = data.get('ws_port', self.DEFAULT_WS_PORT)
                    self.max_volume = data.get('max_volume', self.DEFAULT_MAX_VOLUME)
                    self.volume_check_interval = data.get('volume_check_interval', self.DEFAULT_VOLUME_CHECK_INTERVAL)
                    self.enforce_max_volume = data.get('enforce_max_volume', True)
            except Exception as e:
                print(f"Error loading config: {e}")

    def save_config(self):
        """Save current configuration to file"""
        try:
            data = {
                'ws_host': self.ws_host,
                'ws_port': self.ws_port,
                'max_volume': self.max_volume,
                'volume_check_interval': self.volume_check_interval,
                'enforce_max_volume': self.enforce_max_volume
            }
            with open(self.CONFIG_FILE, 'w') as f:
                json.dump(data, f, indent=4)
        except Exception as e:
            print(f"Error saving config: {e}")

    @property
    def websocket_url(self):
        """Get the full WebSocket URL"""
        return f"ws://{self.ws_host}:{self.ws_port}"
