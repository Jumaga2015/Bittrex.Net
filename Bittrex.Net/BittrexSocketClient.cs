﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bittrex.Net.Objects;
using Bittrex.Net.Interfaces;
using Bittrex.Net.Sockets;
using CryptoExchange.Net;
using CryptoExchange.Net.Logging;
using System.IO;
using System.IO.Compression;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using Newtonsoft.Json.Linq;
using CryptoExchange.Net.Interfaces;

namespace Bittrex.Net
{
    public class BittrexSocketClient: SocketClient//, IBittrexSocketClient
    {
        #region fields
        private static BittrexSocketClientOptions defaultOptions = new BittrexSocketClientOptions();
        private static BittrexSocketClientOptions DefaultOptions
        {
            get
            {
                var result = new BittrexSocketClientOptions()
                {
                    LogVerbosity = defaultOptions.LogVerbosity,
                    BaseAddress = defaultOptions.BaseAddress,
                    LogWriters = defaultOptions.LogWriters,
                    Proxy = defaultOptions.Proxy,
                    ReconnectInterval = defaultOptions.ReconnectInterval
                };

                if (defaultOptions.ApiCredentials != null)
                    result.ApiCredentials = new ApiCredentials(defaultOptions.ApiCredentials.Key.GetString(), defaultOptions.ApiCredentials.Secret.GetString());

                return result;
            }
        }

        private const string HubName = "c2";

        private const string BalanceEvent = "uB";
        private const string MarketEvent = "uE";
        private const string SummaryLiteEvent = "uL";
        private const string SummaryEvent = "uS";
        private const string OrderEvent = "uO";

        private const string SummaryDeltaSub = "SubscribeToSummaryDeltas";
        private const string SummaryLiteDeltaSub = "SubscribeToSummaryLiteDeltas";
        private const string ExchangeDeltaSub = "SubscribeToExchangeDeltas";
        private const string QueryExchangeStateRequest = "QueryExchangeState";
        private const string QuerySummaryStateRequest = "QuerySummaryState";

        public override IWebsocketFactory SocketFactory { get; set; } = new ConnectionFactory();        
        #endregion
        
        #region ctor
        /// <summary>
        /// Creates a new socket client using the default options
        /// </summary>
        public BittrexSocketClient(): this(DefaultOptions)
        {
        }

        /// <summary>
        /// Creates a new socket client using the provided options
        /// </summary>
        /// <param name="options">Options to use for this client</param>
        public BittrexSocketClient(BittrexSocketClientOptions options): base(options, options.ApiCredentials == null ? null : new BittrexAuthenticationProvider(options.ApiCredentials))
        {
            Configure(options);
        }
        #endregion

        #region methods
        #region public
        /// <summary>
        /// Set the default options for new clients
        /// </summary>
        /// <param name="options">Options to use for new clients</param>
        public static void SetDefaultOptions(BittrexSocketClientOptions options)
        {
            defaultOptions = options;
        }

        /// <summary>
        /// Synchronized version of the <see cref="QuerySummaryStatesAsync"/> method
        /// </summary>
        public CallResult<List<BittrexStreamMarketSummary>> QuerySummaryStates() => QuerySummaryStatesAsync().Result;

        ///// <summary>
        ///// Gets the current summaries for all markets
        ///// </summary>
        ///// <returns>Market summaries</returns>
        public async Task<CallResult<List<BittrexStreamMarketSummary>>> QuerySummaryStatesAsync()
        {
            var result = await Query<BittrexStreamMarketSummariesQuery>(new ConnectionRequest(false, QuerySummaryStateRequest));
            return new CallResult<List<BittrexStreamMarketSummary>>(result.Data?.Deltas, result.Error);
        }

        ///// <summary>
        ///// Synchronized version of the <see cref="QueryExchangeStateAsync"/> method
        ///// </summary>
        public CallResult<BittrexStreamQueryExchangeState> QueryExchangeState(string marketName) => QueryExchangeStateAsync(marketName).Result;

        ///// <summary>
        ///// Gets the state of a specific market
        ///// 500 Buys
        ///// 100 Fills
        ///// 500 Sells
        ///// </summary>
        ///// <param name="marketName">The name of the market to query</param>
        ///// <returns>The current exchange state</returns>
        public async Task<CallResult<BittrexStreamQueryExchangeState>> QueryExchangeStateAsync(string marketName)
        {
            return await Query<BittrexStreamQueryExchangeState>(new ConnectionRequest(false, QueryExchangeStateRequest, marketName));
        }

        /// <summary>
        /// Synchronized version of the <see cref="SubscribeToExchangeStateUpdatesAsync"/> method
        /// </summary>
        public CallResult<UpdateSubscription> SubscribeToExchangeStateUpdates(string marketName, Action<BittrexStreamUpdateExchangeState> onUpdate) => SubscribeToExchangeStateUpdatesAsync(marketName, onUpdate).Result;

        /// <summary>
        /// Subscribes to orderbook and trade updates on a specific market
        /// </summary>
        /// <param name="marketName">The name of the market to subscribe on</param>
        /// <param name="onUpdate">The update event handler</param>
        /// <returns>ApiResult whether subscription was successful. The Result property contains the Stream Id which can be used to unsubscribe the stream again</returns>
        public async Task<CallResult<UpdateSubscription>> SubscribeToExchangeStateUpdatesAsync(string marketName, Action<BittrexStreamUpdateExchangeState> onUpdate)
        {
            return await Subscribe(new ConnectionRequest(false, ExchangeDeltaSub, marketName), onUpdate);
        }

        ///// <summary>
        ///// Synchronized version of the <see cref="SubscribeToMarketSummariesUpdateAsync"/> method
        ///// </summary>
        public CallResult<UpdateSubscription> SubscribeToMarketSummariesUpdate(Action<List<BittrexStreamMarketSummary>> onUpdate) => SubscribeToMarketSummariesUpdateAsync(onUpdate).Result;

        ///// <summary>
        ///// Subscribes to updates of summaries for all markets
        ///// </summary>
        ///// <param name="onUpdate">The update event handler</param>
        ///// <returns>ApiResult whether subscription was successful. The Result property contains the Stream Id which can be used to unsubscribe the stream again</returns>
        public async Task<CallResult<UpdateSubscription>> SubscribeToMarketSummariesUpdateAsync(Action<List<BittrexStreamMarketSummary>> onUpdate)
        {
            var inner = new Action<BittrexStreamMarketSummaryUpdate>(data => onUpdate(data.Deltas));
            return await Subscribe(new ConnectionRequest(false, SummaryDeltaSub), inner);
        }

        ///// <summary>
        ///// Synchronized version of the <see cref="SubscribeToMarketSummariesLiteUpdateAsync"/> method
        ///// </summary>
        public CallResult<UpdateSubscription> SubscribeToMarketSummariesLiteUpdate(Action<List<BittrexStreamMarketSummaryLite>> onUpdate) => SubscribeToMarketSummariesLiteUpdateAsync(onUpdate).Result;

        ///// <summary>
        ///// Subscribes to lite summary updates for all markets
        ///// </summary>
        ///// <param name="onUpdate">The update event handler</param>
        ///// <returns>ApiResult whether subscription was successful. The Result property contains the Stream Id which can be used to unsubscribe the stream again</returns>
        public async Task<CallResult<UpdateSubscription>> SubscribeToMarketSummariesLiteUpdateAsync(Action<List<BittrexStreamMarketSummaryLite>> onUpdate)
        {
            var inner = new Action<BittrexStreamMarketSummariesLite>(data => onUpdate(data.Deltas));
            return await Subscribe(new ConnectionRequest(false, SummaryLiteDeltaSub), inner);
        }

        ///// <summary>
        ///// Synchronized version of the <see cref="SubscribeToBalanceUpdatesAsync"/> method
        ///// </summary>
        public CallResult<UpdateSubscription> SubscribeToAccountUpdates(Action<BittrexStreamBalanceData> onBalanceUpdate, Action<BittrexStreamOrderData> onOrderUpdate) => SubscribeToAccountUpdatesAsync(onBalanceUpdate, onOrderUpdate).Result;

        ///// <summary>
        ///// Subscribes to balance updates
        ///// </summary>
        ///// <param name="onUpdate">The update event handler</param>
        ///// <returns>ApiResult whether subscription was successful. The Result property contains the Stream Id which can be used to unsubscribe the stream again</returns>
        public async Task<CallResult<UpdateSubscription>> SubscribeToAccountUpdatesAsync(Action<BittrexStreamBalanceData> onBalanceUpdate, Action<BittrexStreamOrderData> onOrderUpdate)
        {
            var handler = new Action<string>(data =>
            {
                var token = JToken.Parse(data);
                if(token["d"] != null)
                {
                    var desResult = Deserialize<BittrexStreamBalanceData>(token);
                    if (!desResult.Success)
                    {
                        log.Write(LogVerbosity.Warning, "Failed to deserialize balance update: " + desResult.Error);
                        return;
                    }

                    onBalanceUpdate(desResult.Data);
                }
                else
                {
                    var desResult = Deserialize<BittrexStreamOrderData>(token);
                    if (!desResult.Success)
                    {
                        log.Write(LogVerbosity.Warning, "Failed to deserialize order update: " + desResult.Error);
                        return;
                    }

                    onOrderUpdate(desResult.Data);
                }
            });

            return await Subscribe(new ConnectionRequest(true, null), handler);
        }        
        #endregion
        #region private

        private async Task<CallResult<T>> Query<T>(ConnectionRequest request)
        {
            var connectResult = await CreateAndConnectSocket<object>(request.Signed, false, null);
            if (!connectResult.Success)
                return new CallResult<T>(default(T), connectResult.Error);

            var subscription = connectResult.Data;

            var queryResult = await ((ISignalRSocket)subscription.Socket).InvokeProxy<string>(request.RequestName, request.Parameters);
            if (!queryResult.Success)
            {
                var closeTask = subscription.Close();
                return new CallResult<T>(default(T), queryResult.Error);
            }

            var decResult = await DecodeData(queryResult.Data);
            if (!decResult.Success)
            {
                var closeTask = subscription.Close();
                return new CallResult<T>(default(T), decResult.Error);
            }

            var desResult = Deserialize<T>(decResult.Data);
            if (!desResult.Success)
            {
                var closeTask = subscription.Close();
                return new CallResult<T>(default(T), desResult.Error);
            }

            var closeTask2 = subscription.Close();
            return new CallResult<T>(desResult.Data, null);
        }

        private async Task<CallResult<UpdateSubscription>> Subscribe<T>(ConnectionRequest request, Action<T> onData)
        {
            var connectResult = await CreateAndConnectSocket(request.Signed, true, onData);
            if (!connectResult.Success)
                return new CallResult<UpdateSubscription>(null, connectResult.Error);

            return await Subscribe(connectResult.Data, request);
        }


        private async Task<CallResult<UpdateSubscription>> Subscribe(SocketSubscription subscription, ConnectionRequest request)
        {
            if (request.RequestName != null)
            {
                var subResult = await ((ISignalRSocket)subscription.Socket).InvokeProxy<bool>(request.RequestName, request.Parameters);
                if (!subResult.Success || !subResult.Data)
                {
                    var closeTask = subscription.Close();
                    return new CallResult<UpdateSubscription>(null, subResult.Error ?? new ServerError("Subscribe returned false"));
                }
            }

            subscription.Request = request;
            subscription.Socket.ShouldReconnect = true;
            return new CallResult<UpdateSubscription>(new UpdateSubscription(subscription), null);
        }

        private async Task<CallResult<SocketSubscription>> CreateAndConnectSocket<T>(bool authenticated, bool subscribing, Action<T> onData)
        {
            var socket = CreateSocket(baseAddress);
            var subscription = new SocketSubscription(socket);
            if (subscribing)
                subscription.DataHandlers.Add((subs, data) => UpdateHandler(subs, data, onData));            

            var connectResult = await ConnectSocket(subscription);
            if (!connectResult.Success)
                return new CallResult<SocketSubscription>(null, connectResult.Error);

            if(authenticated)
            {
                var authResult = await Authenticate(subscription);
                if (!authResult.Success)
                    return new CallResult<SocketSubscription>(null, authResult.Error);
            }

            return new CallResult<SocketSubscription>(subscription, null);
        }

        protected override IWebsocket CreateSocket(string address)
        {
            var socket = (ISignalRSocket)SocketFactory.CreateWebsocket(log, baseAddress);
            socket.SetHub(HubName);
            log.Write(LogVerbosity.Debug, "Created new socket for " + address);

            if (apiProxy != null)
                socket.SetProxy(apiProxy.Host, apiProxy.Port);

            socket.DataInterpreter = dataInterpreter;
            socket.OnClose += () =>
            {
                SocketOnClose(socket);
            };
            socket.OnError += (e) =>
            {
                log.Write(LogVerbosity.Warning, $"Socket {socket.Id} error: " + e.ToString());
                SocketError(socket, e);
            };
            socket.OnOpen += () =>
            {
                SocketOpened(socket);
            };
            return socket;
        }

        private void AuthUpdateHandler<T>(SocketSubscription subscription, JToken data, Action<string> onData)
        {
            if (data["A"] == null)
                return;

            var decData = DecodeData((string)((JArray)data["A"])[0]).Result;
            if (!decData.Success)
            {
                log.Write(LogVerbosity.Warning, $"Failed to decode data: " + decData.Error);
                return;
            }

            onData(decData.Data);
        }

        private void UpdateHandler<T>(SocketSubscription subscription, JToken data, Action<T> onData)
        {
            if (data["A"] == null)
                return;

            var decData = DecodeData((string)((JArray)data["A"])[0]).Result;
            if (!decData.Success)
            {
                log.Write(LogVerbosity.Warning, $"Failed to decode data: " + decData.Error);
                return;
            }

            if(typeof(T) == typeof(string))
            {
                onData((T)Convert.ChangeType(decData.Data, typeof(T)));
                return;
            }

            var desData = Deserialize<T>(decData.Data);
            if (!desData.Success)
            {
                log.Write(LogVerbosity.Warning, $"Failed to deserialize data into {typeof(T).Name}: " + desData.Error);
                return;
            }

            onData(desData.Data);
        }

        private async Task<CallResult<bool>> Authenticate(SocketSubscription subscription)
        {
            if (authProvider == null)
                return new CallResult<bool>(false, new NoApiCredentialsError());

            log.Write(LogVerbosity.Debug, "Starting authentication");
            var socket = (ISignalRSocket)subscription.Socket;
            var result = await socket.InvokeProxy<string>("GetAuthContext", authProvider.Credentials.Key.GetString()).ConfigureAwait(false);
            if (!result.Success)
            {
                log.Write(LogVerbosity.Error, "Authentication failed, api key is probably invalid");
                return new CallResult<bool>(false, result.Error);
            }

            log.Write(LogVerbosity.Debug, "Auth context retrieved");
            var signed = authProvider.Sign(result.Data);
            var authResult = await socket.InvokeProxy<bool>("Authenticate", authProvider.Credentials.Key.GetString(), signed).ConfigureAwait(false);
            if (!authResult.Success || !authResult.Data)
            {
                log.Write(LogVerbosity.Error, "Authentication failed, api secret is probably invalid");
                return new CallResult<bool>(false, authResult.Error ?? new ServerError("Authentication failed"));
            }

            log.Write(LogVerbosity.Info, "Authentication successful");
            return new CallResult<bool>(true, null);
        }

        protected override void ProcessMessage(SocketSubscription subscription, string data)
        {
            foreach (var handler in subscription.DataHandlers)
                handler(subscription, JToken.Parse(data));
        }

        private async Task<CallResult<string>> DecodeData(string rawData)
        {
            try
            {
                byte[] gzipData = Convert.FromBase64String(rawData);
                using (var decompressedStream = new MemoryStream())
                using (var compressedStream = new MemoryStream(gzipData))
                using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    deflateStream.CopyTo(decompressedStream);
                    decompressedStream.Position = 0;

                    using (var streamReader = new StreamReader(decompressedStream))
                    {
                        var data = await streamReader.ReadToEndAsync().ConfigureAwait(false);
                        log.Write(LogVerbosity.Debug, "Socket received data: " + data);
                        if (data == "null")
                            return new CallResult<string>(null, new DeserializeError("Server returned null"));

                        return new CallResult<string>(data, null);
                    }
                }
            }
            catch (Exception e)
            {
                log.Write(LogVerbosity.Info, "Exception in decode data: " + e.Message);
                return new CallResult<string>(null, new DeserializeError("Exception in decode data: " + e.Message));
            }
        }

        protected override bool SocketReconnect(SocketSubscription subscription, TimeSpan disconnectedTime)
        {
            var request = (ConnectionRequest)subscription.Request;
            if (request.Signed)
            {
                if (!Authenticate(subscription).Result.Success)
                    return false;
            }

            return Subscribe(subscription, request).Result.Success;
        }
        #endregion
        #endregion
    }
}
