using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using System.Net.WebSockets;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

using static WebSocketMessages;
using static Utils;

namespace WebSocketExample
{

    public class Vector2
    {
        public int X;
        public int Y;
    }

    public class Startup
    {
        private Dictionary<Guid, List<WebSocket>> _lobbys = 
            new Dictionary<Guid, List<WebSocket>>();

        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseFileServer(enableDirectoryBrowsing: true);
            app.UseWebSockets(); // Only for Kestrel
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}"
                );
            });


            app.Map("/lobby", builder =>
            {
                builder.Use(async (context, next) =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        Console.WriteLine("accepted web socket");
                        await RunServer(webSocket);
                        return;
                    }
                    await next();
                });
            });
        }

        private async Task RunServer(WebSocket webSocket)
        {
            byte[] buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                int msgId = BitConverter.ToInt32(buffer, 0);
                var answer = await GetAnswer(msgId, buffer, webSocket);

                if (answer != null)
                {
                    await webSocket.SendAsync(new ArraySegment<byte>(answer), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }

                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        public static byte[] SubArray(byte[] data, int index, int length)
        {
            byte[] result = new byte[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        private async Task<byte[]> GetAnswer(int msgId, byte[] buffer, WebSocket webSocket)
        {
            if (msgId == CreateLobbyRequest)
            {
                Guid GUID = Guid.NewGuid();
                Console.WriteLine("new Guid is " + BitConverter.ToString(GUID.ToByteArray()));
                _lobbys.Add(GUID, new List<WebSocket>() { webSocket } );
                return MergeArrays(BitConverter.GetBytes(LobbyCreated), GUID.ToByteArray());
            }

            if (msgId == JoinLobbyRequest)
            {
                Guid GUID = new Guid(SubArray(buffer, 4, 16));
                Console.WriteLine("received new client with guid " + BitConverter.ToString(GUID.ToByteArray()));
                _lobbys[GUID].Add(webSocket);

                var msg = 
                    MergeArrays
                    (
                        BitConverter.GetBytes(GameStart),
                        GUID.ToByteArray()
                    );

                foreach(var wb in _lobbys[GUID]) 
                {
                    await wb.SendAsync
                    (
                        new ArraySegment<byte>(msg),
                        WebSocketMessageType.Binary, 
                        true, 
                        CancellationToken.None
                    );
                }
            }

            if (msgId == MoveCommand) 
            {
                Guid GUID = new Guid(SubArray(buffer, 4, 16));
                var playerId_bytes = SubArray(buffer, 20, 4);
                var pos_x_bytes = SubArray(buffer, 24, 4);
                var pos_y_bytes = SubArray(buffer, 28, 4);

                var playerId = BitConverter.ToInt32(playerId_bytes);
                var pos_x = BitConverter.ToInt32(pos_x_bytes);
                var pos_y = BitConverter.ToInt32(pos_y_bytes);

                var msg = 
                    MergeArrays
                    (
                        MergeArrays
                        (
                            MergeArrays
                            (
                                BitConverter.GetBytes(UpdateGameState),
                                playerId_bytes
                            ),
                            pos_x_bytes
                        ),
                        pos_y_bytes
                    );

                foreach(var wb in _lobbys[GUID]) 
                {
                    await wb.SendAsync
                    (
                        new ArraySegment<byte>(msg),
                        WebSocketMessageType.Binary, 
                        true, 
                        CancellationToken.None
                    );
                }

            }

            return null;
        }
    }
}
