﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Extensions;
using Rebus.Handlers;
using Rebus.Transport;
using Microsoft.AspNetCore.Hosting;

namespace Rebus.ServiceProvider
{
    /// <summary>
    /// Implementation of <see cref="IContainerAdapter"/> that is backed by a ServiceProvider
    /// </summary>
    /// <seealso cref="Rebus.Activation.IContainerAdapter" />
    public class ServiceProviderContainerAdapter : IContainerAdapter
    {
        private readonly IServiceProvider _provider;

        private IBus _bus;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceProviderContainerAdapter"/> class.
        /// </summary>
        /// <param name="provider">The service provider used to yield handler instances.</param>
        public ServiceProviderContainerAdapter(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));

            var applicationLifetime = _provider.GetService<IApplicationLifetime>();
            applicationLifetime?.ApplicationStopping.Register(Dispose);
        }

        /// <summary>
        /// Resolves all handlers for the given <typeparamref name="TMessage"/> message type
        /// </summary>
        /// <exception cref="System.InvalidOperationException"></exception>
        public Task<IEnumerable<IHandleMessages<TMessage>>> GetHandlers<TMessage>(TMessage message, ITransactionContext transactionContext)
        {
            if (_bus == null)
            {
                throw new InvalidOperationException($"Rebus messaging engine has not been activated yet, no messages can be processed.");
            }

            var resolvedHandlerInstances = GetMessageHandlersForMessage<TMessage>();

            transactionContext.OnDisposed(() =>
            {
                foreach (var disposableInstance in resolvedHandlerInstances.OfType<IDisposable>())
                {
                    disposableInstance.Dispose();
                }
            });

            return Task.FromResult((IEnumerable<IHandleMessages<TMessage>>)resolvedHandlerInstances.ToArray());
        }

        /// <summary>
        /// Sets the bus instance associated with this <see cref="T:Rebus.Activation.IContainerAdapter" />.
        /// </summary>
        /// <param name="bus"></param>
        public void SetBus(IBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        List<IHandleMessages<TMessage>> GetMessageHandlersForMessage<TMessage>()
        {
            var handledMessageTypes = typeof(TMessage).GetBaseTypes()
                .Concat(new[] { typeof(TMessage) });

            return handledMessageTypes
                .SelectMany(t =>
                {
                    var implementedInterface = typeof(IHandleMessages<>).MakeGenericType(t);

                    return _provider.GetServices(implementedInterface).Cast<IHandleMessages>();
                })
                .Cast<IHandleMessages<TMessage>>()
                .ToList();
        }

        #region IDisposable Support

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _bus?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
