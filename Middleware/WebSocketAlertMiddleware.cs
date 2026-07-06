using System.Net.WebSockets;

namespace VitalBand.Middleware
{
    public class WebSocketAlertMiddleware
    {
        private readonly RequestDelegate _next;

        public WebSocketAlertMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path == "/api/alertas")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    if (int.TryParse(context.Request.Query["pacienteId"], out int pacienteId))
                    {
                        using WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();

                        WebSocketConnectionManager.AddSocket(pacienteId, webSocket);

                        await KeepSocketAliveAsync(webSocket, pacienteId);
                        return;
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
            }

            await _next(context);
        }

        private static async Task KeepSocketAliveAsync(WebSocket webSocket, int pacienteId)
        {
            var buffer = new byte[1024 * 4];

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                await WebSocketConnectionManager.RemoveSocket(pacienteId);
            }
        }
    }
}