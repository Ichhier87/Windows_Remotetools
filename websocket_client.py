"""
WebSocket Client for receiving remote commands
"""
import asyncio
import json
import websockets
import threading


class WebSocketClient:
    """WebSocket client that receives and processes commands"""

    def __init__(self, config, volume_controller, overlay_window):
        self.config = config
        self.volume_controller = volume_controller
        self.overlay_window = overlay_window
        self.websocket = None
        self.running = False
        self.connected = False
        self._thread = None
        self._loop = None

    def start(self):
        """Start WebSocket client in background thread"""
        if not self.running:
            self.running = True
            self._thread = threading.Thread(target=self._run_async, daemon=True)
            self._thread.start()
            print("WebSocket client started")

    def stop(self):
        """Stop WebSocket client"""
        self.running = False
        self.connected = False
        if self._loop:
            self._loop.call_soon_threadsafe(self._loop.stop)
        print("WebSocket client stopped")

    def _run_async(self):
        """Run async event loop in thread"""
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        self._loop.run_until_complete(self._connect_loop())

    async def _connect_loop(self):
        """Main connection loop with reconnection"""
        while self.running:
            try:
                print(f"Connecting to {self.config.websocket_url}...")
                async with websockets.connect(self.config.websocket_url) as websocket:
                    self.websocket = websocket
                    self.connected = True
                    print("WebSocket connected!")
                    await self._receive_loop()
            except Exception as e:
                print(f"WebSocket connection error: {e}")
                self.connected = False
                if self.running:
                    print("Reconnecting in 5 seconds...")
                    await asyncio.sleep(5)

    async def _receive_loop(self):
        """Receive and process messages"""
        try:
            async for message in self.websocket:
                await self._process_message(message)
        except websockets.exceptions.ConnectionClosed:
            print("WebSocket connection closed")
            self.connected = False
        except Exception as e:
            print(f"Error in receive loop: {e}")
            self.connected = False

    async def _process_message(self, message):
        """
        Process received WebSocket message

        Expected JSON format:
        {
            "command": "command_name",
            "params": {
                "param1": "value1",
                ...
            }
        }
        """
        try:
            data = json.loads(message)
            command = data.get('command', '')
            params = data.get('params', {})

            print(f"Received command: {command} with params: {params}")

            # Execute command
            self._execute_command(command, params)

        except json.JSONDecodeError:
            print(f"Invalid JSON received: {message}")
        except Exception as e:
            print(f"Error processing message: {e}")

    def _execute_command(self, command, params):
        """Execute a command based on the command name"""

        # Overlay commands
        if command == "show_overlay":
            message = params.get('message', '')
            bg_color = params.get('bg_color', 'black')
            text_color = params.get('text_color', 'white')
            opacity = params.get('opacity', 0.9)
            self.overlay_window.show_async(message, bg_color, text_color, opacity)

        elif command == "hide_overlay":
            self.overlay_window.hide()

        elif command == "show_blocking_screen":
            message = params.get('message', 'Screen Locked')
            self.overlay_window.show_blocking_screen(message)

        elif command == "show_notification":
            message = params.get('message', 'Notification')
            self.overlay_window.show_notification(message)

        elif command == "show_warning":
            message = params.get('message', 'Warning')
            self.overlay_window.show_warning(message)

        elif command == "show_error":
            message = params.get('message', 'Error')
            self.overlay_window.show_error(message)

        # Volume commands
        elif command == "set_volume":
            volume = params.get('volume', 50)
            self.volume_controller.set_volume_percent(volume)

        elif command == "get_volume":
            volume = self.volume_controller.get_volume_percent()
            print(f"Current volume: {volume}%")
            # Could send response back through websocket

        elif command == "set_mute":
            mute = params.get('mute', True)
            self.volume_controller.set_mute(mute)

        elif command == "toggle_mute":
            self.volume_controller.toggle_mute()

        elif command == "set_max_volume":
            max_volume = params.get('max_volume', 100)
            self.volume_controller.set_max_volume_limit(max_volume)

        elif command == "enable_volume_enforcement":
            enable = params.get('enable', True)
            self.volume_controller.enable_enforcement(enable)

        # General commands
        elif command == "ping":
            print("Pong!")
            # Could send response back

        else:
            print(f"Unknown command: {command}")

    async def send_message(self, message):
        """Send a message through WebSocket"""
        if self.connected and self.websocket:
            try:
                await self.websocket.send(json.dumps(message))
            except Exception as e:
                print(f"Error sending message: {e}")

    def get_status(self):
        """Get connection status"""
        return "Connected" if self.connected else "Disconnected"
