"""
Example WebSocket Server for testing the Windows Remote Tools client

This server demonstrates how to send commands to the client application.
Run this server and use the test functions to send commands.
"""
import asyncio
import websockets
import json


# Connected clients
connected_clients = set()


async def handler(websocket, path):
    """Handle WebSocket connections"""
    # Register client
    connected_clients.add(websocket)
    print(f"Client connected. Total clients: {len(connected_clients)}")

    try:
        # Keep connection alive
        async for message in websocket:
            print(f"Received from client: {message}")
            # Echo back or handle client messages
            await websocket.send(json.dumps({"status": "received", "message": message}))

    except websockets.exceptions.ConnectionClosed:
        pass
    finally:
        # Unregister client
        connected_clients.remove(websocket)
        print(f"Client disconnected. Total clients: {len(connected_clients)}")


async def send_command(command, params=None):
    """Send a command to all connected clients"""
    if not connected_clients:
        print("No clients connected!")
        return

    message = {
        "command": command,
        "params": params or {}
    }

    print(f"Sending command: {command}")
    websockets.broadcast(connected_clients, json.dumps(message))


async def interactive_menu():
    """Interactive menu for sending commands"""
    await asyncio.sleep(2)  # Wait for server to start

    print("\n" + "="*60)
    print("WebSocket Server Running - Interactive Command Menu")
    print("="*60)

    while True:
        print("\nAvailable Commands:")
        print("1. Show Overlay")
        print("2. Show Blocking Screen")
        print("3. Show Notification")
        print("4. Hide Overlay")
        print("5. Set Volume to 50%")
        print("6. Set Volume to 100%")
        print("7. Mute")
        print("8. Unmute")
        print("9. Set Max Volume to 50%")
        print("10. Set Max Volume to 75%")
        print("11. Enable Volume Enforcement")
        print("12. Disable Volume Enforcement")
        print("13. Ping")
        print("0. Quit")

        choice = await asyncio.get_event_loop().run_in_executor(
            None, input, "\nEnter command number: "
        )

        if choice == "1":
            message = await asyncio.get_event_loop().run_in_executor(
                None, input, "Enter message: "
            )
            await send_command("show_overlay", {
                "message": message,
                "bg_color": "darkblue",
                "text_color": "white",
                "opacity": 0.9
            })

        elif choice == "2":
            message = await asyncio.get_event_loop().run_in_executor(
                None, input, "Enter message (default: Screen Locked): "
            )
            await send_command("show_blocking_screen", {
                "message": message or "Screen Locked"
            })

        elif choice == "3":
            message = await asyncio.get_event_loop().run_in_executor(
                None, input, "Enter notification: "
            )
            await send_command("show_notification", {
                "message": message
            })

        elif choice == "4":
            await send_command("hide_overlay")

        elif choice == "5":
            await send_command("set_volume", {"volume": 50})

        elif choice == "6":
            await send_command("set_volume", {"volume": 100})

        elif choice == "7":
            await send_command("set_mute", {"mute": True})

        elif choice == "8":
            await send_command("set_mute", {"mute": False})

        elif choice == "9":
            await send_command("set_max_volume", {"max_volume": 50})

        elif choice == "10":
            await send_command("set_max_volume", {"max_volume": 75})

        elif choice == "11":
            await send_command("enable_volume_enforcement", {"enable": True})

        elif choice == "12":
            await send_command("enable_volume_enforcement", {"enable": False})

        elif choice == "13":
            await send_command("ping")

        elif choice == "0":
            print("Shutting down server...")
            break

        else:
            print("Invalid choice!")


async def main():
    """Main server function"""
    host = "localhost"
    port = 8765

    print(f"Starting WebSocket server on {host}:{port}...")

    # Start server
    async with websockets.serve(handler, host, port):
        print(f"Server running on ws://{host}:{port}")
        print("Waiting for clients to connect...")

        # Run interactive menu
        try:
            await interactive_menu()
        except KeyboardInterrupt:
            print("\nServer stopped by user")


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\nShutdown complete")
