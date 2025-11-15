"""
Fullscreen Overlay Window for displaying messages or blocking screen
"""
import tkinter as tk
from tkinter import font


class OverlayWindow:
    """Fullscreen overlay window that can display messages"""

    def __init__(self):
        self.window = None
        self.is_showing = False

    def show(self, message="", bg_color="black", text_color="white", opacity=0.9):
        """
        Show fullscreen overlay window

        Args:
            message: Text message to display (optional)
            bg_color: Background color (default: black)
            text_color: Text color (default: white)
            opacity: Window opacity 0.0-1.0 (default: 0.9)
        """
        if self.is_showing:
            self.hide()

        self.window = tk.Tk()
        self.window.title("Remote Control Overlay")

        # Configure fullscreen
        self.window.attributes('-fullscreen', True)
        self.window.attributes('-topmost', True)
        self.window.attributes('-alpha', opacity)

        # Set background color
        self.window.configure(bg=bg_color)

        # Create main frame
        frame = tk.Frame(self.window, bg=bg_color)
        frame.pack(expand=True, fill='both')

        if message:
            # Display message
            msg_font = font.Font(family='Arial', size=48, weight='bold')
            label = tk.Label(
                frame,
                text=message,
                font=msg_font,
                fg=text_color,
                bg=bg_color,
                wraplength=1200
            )
            label.pack(expand=True)

        # Add close instruction
        close_font = font.Font(family='Arial', size=16)
        close_label = tk.Label(
            frame,
            text="Press ESC to close",
            font=close_font,
            fg=text_color,
            bg=bg_color
        )
        close_label.pack(side='bottom', pady=20)

        # Bind ESC key to close
        self.window.bind('<Escape>', lambda e: self.hide())

        # Bind click to close
        self.window.bind('<Button-1>', lambda e: self.hide())

        self.is_showing = True

        # Start event loop
        self.window.mainloop()

    def show_async(self, message="", bg_color="black", text_color="white", opacity=0.9):
        """Show overlay in a separate thread"""
        import threading
        thread = threading.Thread(
            target=self.show,
            args=(message, bg_color, text_color, opacity),
            daemon=True
        )
        thread.start()

    def hide(self):
        """Hide the overlay window"""
        if self.window and self.is_showing:
            try:
                self.window.quit()
                self.window.destroy()
            except:
                pass
            finally:
                self.window = None
                self.is_showing = False

    def show_blocking_screen(self, message="Screen Locked"):
        """Show a blocking screen overlay (dark overlay)"""
        self.show_async(message=message, bg_color="black", text_color="red", opacity=0.95)

    def show_notification(self, message):
        """Show a notification overlay (semi-transparent)"""
        self.show_async(message=message, bg_color="blue", text_color="white", opacity=0.7)

    def show_warning(self, message):
        """Show a warning overlay"""
        self.show_async(message=message, bg_color="orange", text_color="black", opacity=0.85)

    def show_error(self, message):
        """Show an error overlay"""
        self.show_async(message=message, bg_color="red", text_color="white", opacity=0.9)
