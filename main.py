"""
Windows Remote Tools - Tray Application

Main entry point for the application.
This application runs in the system tray and can be controlled via WebSocket commands.

Features:
- System tray icon with menu
- WebSocket command receiver
- Volume control with maximum limit enforcement
- Fullscreen overlay window
"""
import sys
from tray_app import TrayApp


def main():
    """Main entry point"""
    try:
        app = TrayApp()
        app.run()
    except KeyboardInterrupt:
        print("\nShutdown requested...")
        sys.exit(0)
    except Exception as e:
        print(f"Fatal error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
