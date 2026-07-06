using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

namespace VitalBand.Middleware
{
    public static class WebSocketConnectionManager
    {
        private static readonly ConcurrentDictionary<int, WebSocket> _sockets = new();

        public static void AddSocket(int pacienteId, WebSocket socket)
        {
            _sockets.AddOrUpdate(pacienteId, socket, (_, oldSocket) => socket);
        }

        public static ConcurrentDictionary<int, WebSocket> GetAllSockets()
        {
            return _sockets;
        }

        public static async Task RemoveSocket(int pacienteId)
        {
            if (_sockets.TryRemove(pacienteId, out var socket))
            {
                if (socket.State == WebSocketState.Open || socket.State == WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexión cerrada", CancellationToken.None);
                }
            }
        }

        public static async Task SendMessageAsync(int pacienteId, string message)
        {
            if (_sockets.TryGetValue(pacienteId, out var socket))
            {
                if (socket.State == WebSocketState.Open)
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    var buffer = new ArraySegment<byte>(bytes);
                    await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
}