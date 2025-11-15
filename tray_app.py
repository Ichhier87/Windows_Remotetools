"""
System Tray Application
"""
import pystray
from PIL import Image, ImageDraw
from config import Config
from volume_controller import VolumeController
from overlay_window import OverlayWindow
from websocket_client import WebSocketClient


class TrayApp:
    """Main system tray application"""

    def __init__(self):
        self.config = Config()
        self.volume_controller = None
        self.overlay_window = OverlayWindow()
        self.websocket_client = None
        self.icon = None

        # Initialize components
        self._init_components()

    def _init_components(self):
        """Initialize all components"""
        try:
            self.volume_controller = VolumeController(self.config)
            self.websocket_client = WebSocketClient(
                self.config,
                self.volume_controller,
                self.overlay_window
            )
        except Exception as e:
            print(f"Error initializing components: {e}")
            raise

    def create_icon_image(self):
        """Create a simple icon image"""
        # Create a simple colored square as icon
        width = 64
        height = 64
        color1 = "blue"
        color2 = "lightblue"

        image = Image.new('RGB', (width, height), color1)
        dc = ImageDraw.Draw(image)

        # Draw a simple pattern
        dc.rectangle([10, 10, 54, 54], fill=color2)
        dc.rectangle([20, 20, 44, 44], fill=color1)

        return image

    def create_menu(self):
        """Create the system tray menu"""
        return pystray.Menu(
            pystray.MenuItem(
                f"Status: {self.websocket_client.get_status()}",
                lambda: None,
                enabled=False
            ),
            pystray.Menu.SEPARATOR,

            # WebSocket controls
            pystray.MenuItem(
                "WebSocket",
                pystray.Menu(
                    pystray.MenuItem(
                        "Connect" if not self.websocket_client.connected else "Disconnect",
                        self._toggle_websocket
                    ),
                    pystray.MenuItem(
                        f"Server: {self.config.websocket_url}",
                        lambda: None,
                        enabled=False
                    ),
                )
            ),

            # Volume controls
            pystray.MenuItem(
                "Volume",
                pystray.Menu(
                    pystray.MenuItem(
                        f"Current: {self.volume_controller.get_volume_percent()}%",
                        lambda: None,
                        enabled=False
                    ),
                    pystray.MenuItem(
                        f"Max Limit: {self.config.max_volume}%",
                        lambda: None,
                        enabled=False
                    ),
                    pystray.Menu.SEPARATOR,
                    pystray.MenuItem(
                        "Mute",
                        self._toggle_mute,
                        checked=lambda item: self.volume_controller.get_mute()
                    ),
                    pystray.MenuItem(
                        "Enforce Max Volume",
                        self._toggle_volume_enforcement,
                        checked=lambda item: self.config.enforce_max_volume
                    ),
                    pystray.Menu.SEPARATOR,
                    pystray.MenuItem(
                        "Set Max to 50%",
                        lambda: self._set_max_volume(50)
                    ),
                    pystray.MenuItem(
                        "Set Max to 75%",
                        lambda: self._set_max_volume(75)
                    ),
                    pystray.MenuItem(
                        "Set Max to 100%",
                        lambda: self._set_max_volume(100)
                    ),
                )
            ),

            # Overlay controls
            pystray.MenuItem(
                "Overlay",
                pystray.Menu(
                    pystray.MenuItem(
                        "Show Test Overlay",
                        self._test_overlay
                    ),
                    pystray.MenuItem(
                        "Show Blocking Screen",
                        lambda: self.overlay_window.show_blocking_screen("Test Lock")
                    ),
                    pystray.MenuItem(
                        "Hide Overlay",
                        lambda: self.overlay_window.hide()
                    ),
                )
            ),

            pystray.Menu.SEPARATOR,

            # Monitoring
            pystray.MenuItem(
                "Volume Monitoring",
                self._toggle_monitoring,
                checked=lambda item: self.volume_controller._monitoring
            ),

            pystray.Menu.SEPARATOR,

            # Exit
            pystray.MenuItem(
                "Exit",
                self._quit
            ),
        )

    def _toggle_websocket(self, icon, item):
        """Toggle WebSocket connection"""
        if self.websocket_client.connected:
            self.websocket_client.stop()
        else:
            self.websocket_client.start()
        # Update menu
        icon.menu = self.create_menu()

    def _toggle_mute(self, icon, item):
        """Toggle mute"""
        self.volume_controller.toggle_mute()
        icon.menu = self.create_menu()

    def _toggle_volume_enforcement(self, icon, item):
        """Toggle volume enforcement"""
        self.volume_controller.enable_enforcement(not self.config.enforce_max_volume)
        icon.menu = self.create_menu()

    def _set_max_volume(self, max_vol):
        """Set maximum volume limit"""
        self.volume_controller.set_max_volume_limit(max_vol)
        if self.icon:
            self.icon.menu = self.create_menu()

    def _toggle_monitoring(self, icon, item):
        """Toggle volume monitoring"""
        if self.volume_controller._monitoring:
            self.volume_controller.stop_monitoring()
        else:
            self.volume_controller.start_monitoring()
        icon.menu = self.create_menu()

    def _test_overlay(self, icon, item):
        """Show test overlay"""
        self.overlay_window.show_async(
            message="Test Overlay\n\nThis is a test message",
            bg_color="darkblue",
            text_color="white"
        )

    def _quit(self, icon, item):
        """Quit the application"""
        print("Shutting down...")

        # Stop all components
        if self.websocket_client:
            self.websocket_client.stop()

        if self.volume_controller:
            self.volume_controller.stop_monitoring()

        if self.overlay_window:
            self.overlay_window.hide()

        # Stop icon
        icon.stop()

    def run(self):
        """Run the tray application"""
        print(f"Starting {self.config.APP_NAME}...")

        # Start WebSocket client
        self.websocket_client.start()

        # Start volume monitoring if enabled
        if self.config.enforce_max_volume:
            self.volume_controller.start_monitoring()

        # Create and run system tray icon
        self.icon = pystray.Icon(
            self.config.APP_NAME,
            self.create_icon_image(),
            self.config.APP_NAME,
            self.create_menu()
        )

        print("Tray icon started. Application running...")
        self.icon.run()
