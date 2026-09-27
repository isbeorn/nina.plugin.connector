using NINA.Equipment.Equipment;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NINA.Plugins.Connector
{
    internal class MediatorWrapper
    {
        private object _mediator;
        private readonly Type _mediatorType;

        public MediatorWrapper(object mediator)
        {
            _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
            _mediatorType = _mediator.GetType();
        }

        public DeviceInfo GetInfo() => ExecuteFunction<DeviceInfo>("GetInfo");

        public async Task<bool> Connect() => await ExecuteFunctionAsync<bool>("Connect");

        public async Task<bool> Disconnect() => await ExecuteFunctionAsync<bool>("Disconnect");

        public async Task<IList<string>> Rescan() => await ExecuteFunctionAsync<IList<string>>("Rescan");

        private T ExecuteFunction<T>(string functionName, params object[] parameters)
        {
            var method = _mediatorType.GetMethod(functionName);

            if (method == null)
                throw new InvalidOperationException($"Method '{functionName}' not found on mediator of type '{_mediatorType.Name}'.");

            var result = method.Invoke(_mediator, parameters);

            return result is T typedResult
                ? typedResult
                : throw new InvalidOperationException($"Method '{functionName}' did not return a value of type '{typeof(T).Name}'.");
        }

        private async Task<T> ExecuteFunctionAsync<T>(string functionName, params object[] parameters)
        {
            var method = _mediatorType.GetMethod(functionName);

            if (method == null)
                throw new InvalidOperationException($"Method '{functionName}' not found on mediator of type '{_mediatorType.Name}'.");

            var result = method.Invoke(_mediator, parameters);

            return result is Task<T> taskResult
                ? await taskResult
                : throw new InvalidOperationException($"Method '{functionName}' did not return a Task<{typeof(T).Name}>.");
        }
    }
}
