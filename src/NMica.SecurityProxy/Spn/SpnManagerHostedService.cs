﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kerberos.NET.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NMica.SecurityProxy.Middleware
{
    public class SpnManagerHostedService : IHostedService
    {
        private readonly IRouteProvider _routeProvider;
        private readonly ISpnClient _spnClient;
        private readonly ILogger<SpnManagerHostedService> _logger;
        private readonly SpnManagerOptions _options;
        public SpnManagerHostedService(IOptions<SpnManagerOptions> options, IRouteProvider routeProvider, ISpnClient spnClient, ILogger<SpnManagerHostedService> logger)
        {
            _routeProvider = routeProvider;
            _spnClient = spnClient;
            _logger = logger;
            _options = options.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.Enabled!.Value)
            {
                return;
            }

            try
            {
                var urls = await _routeProvider.GetRoutes();
                var spns = urls.SelectMany(x => GetSpns(new Uri(x))).ToHashSet();
                var existingSpns = (await _spnClient.GetAllSpn(cancellationToken)).ToHashSet();
                var spnsToAdd = new HashSet<string>(spns);
                spnsToAdd.ExceptWith(existingSpns);

                foreach (var spn in spns)
                {
                    await _spnClient.AddSpn(spn);
                    _logger.LogInformation("SPN {spn} created", spn);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to manage SPNs");
            }
        }

        private static IEnumerable<string> GetSpns(Uri uri)
        {
            yield return $"http/{uri.Host}:{uri.Port}";
            if (uri.IsDefaultPort)
            {
                yield return $"http/{uri.Host}";
            }
        }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}