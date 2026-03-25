using FactoryClient.Models;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace FactoryClient.Services
{
    public class CameraHubService
    {
        private HubConnection? _connection;

        public event Action<CameraControlStatusDto>? CameraStatusChanged;

        public async Task StartAsync()
        {
            if (_connection != null)
                return;

            _connection = new HubConnectionBuilder()
                .WithUrl("https://localhost:7125/hubs/camera", options =>
                {
                    options.HttpMessageHandlerFactory = handler =>
                    {
                        if (handler is HttpClientHandler clientHandler)
                        {
                            clientHandler.ServerCertificateCustomValidationCallback =
                                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                        }
                        return handler;
                    };
                })
                .WithAutomaticReconnect()
                .Build();

            _connection.On<CameraControlStatusDto>("CameraStatusChanged", payload =>
            {
                CameraStatusChanged?.Invoke(payload);
            });

            await _connection.StartAsync();
        }

        public async Task StopAsync()
        {
            if (_connection == null)
                return;

            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}