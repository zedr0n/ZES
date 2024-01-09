using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog.Fluent;
using NodaTime;
using NodaTime.Extensions;
using SimpleInjector;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure;
using ZES.Infrastructure.Alerts;
using ZES.Infrastructure.Domain;
using ZES.Infrastructure.Net;
using ZES.Infrastructure.Utils;
using ZES.Interfaces;
using ZES.Interfaces.Branching;
using ZES.Interfaces.Causality;
using ZES.Interfaces.Clocks;
using ZES.Interfaces.Domain;
using ZES.Interfaces.EventStore;
using ZES.Interfaces.Net;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain;
using ZES.Tests.Domain.Commands;
using ZES.Tests.Domain.Queries;
using ZES.Tests.Domain.Sagas;
using ZES.Utils;

namespace ZES.Tests
{
    public class InfraTests : ZesTest
    {
        public InfraTests(ITestOutputHelper outputHelper)
            : base(outputHelper)
        {
        }
        
        [Fact]
        public async void CanSaveRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();
            var id = $"{nameof(CanSaveRoot)}-Root";
            
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var root = await repository.Find<Root>(id);
            Assert.Equal(id, root.Id);
        }

        [Fact]
        public async void CanCalculateStreamHash()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var store = container.GetInstance<IEventStore<IAggregate>>();
            var locator = container.GetInstance<IStreamLocator>();

            var id = $"{nameof(CanCalculateStreamHash)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            var stream = await locator.Find<Root>(id);
            var events = await store.ReadStream<IEvent>(stream, 0).ToList();
            Assert.Single(events);
            Assert.NotNull(events.Single().StreamHash);

            var streamHash = await store.GetHash(stream);
            Assert.Equal(events.Single().StreamHash, streamHash);

            await await bus.CommandAsync(new UpdateRoot(id));
            stream = await locator.Find<Root>(id);
            var otherHash = await store.GetHash(stream, 0);
            Assert.Equal(streamHash, otherHash);
        }

        [Fact]
        public async void CanCreateRecord()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanCreateRecord)}-Root";
            await await bus.CommandAsync(new CreateRecord(id));

            var record = await repository.Find<Domain.Record>(id);
            Assert.Equal(id, record.Id);

            await await bus.CommandAsync(new AddRecord(id, 1));
            record = await repository.Find<Domain.Record>(id);
            Assert.Equal(1, record.Values.FirstOrDefault().Value);
        }

        [Fact]
        public async void CanCalculateTotalRecord()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanCalculateTotalRecord)}-Root";
            await await bus.CommandAsync(new CreateRecord(id));

            await await bus.CommandAsync(new AddRecord(id, 1));
            await bus.Equal(new TotalRecordQuery(), r => r.Total, 1.0);

            await await bus.CommandAsync(new AddRecord(id, 2));
            await bus.Equal(new TotalRecordQuery(), r => r.Total, 3.0);
        }

        [Fact]
        public async void CannotSaveTwice()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var errorLog = container.GetInstance<IErrorLog>();
            var commandLog = container.GetInstance<ICommandLog>();

            IError error = null;
            errorLog.Observable.Subscribe(e => error = e);

            var id = $"{nameof(CannotSaveTwice)}-Root";
            var command = new CreateRoot(id) { StoreInLog = false };
            await await bus.CommandAsync(command);
            await bus.Command(command, 2);
            Assert.Equal(nameof(InvalidOperationException), error.ErrorType); 
            Assert.Contains("ahead", error.Message);
            Assert.NotEqual(default, error.Timestamp);

            var failedCommands = await commandLog.FailedCommands.FirstAsync();
            Assert.Single(failedCommands);
        }

        [Fact]
        public async void CanDetectDuplicateCommands()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var errorLog = container.GetInstance<IErrorLog>();
 
            IError error = null;
            errorLog.Observable.Subscribe(e => error = e);
            var id = $"{nameof(CanDetectDuplicateCommands)}-Root";
            var command = new CreateRoot(id);
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(command);
            Assert.NotNull(error); 
        }

        [Fact]
        public async void CanSaveMultipleRoots()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanSaveMultipleRoots)}-Root";
            var command = new CreateRoot($"{id}1");
            await bus.CommandAsync(command);  
            
            var command2 = new CreateRoot($"{id}2");
            await bus.CommandAsync(command2);

            var root = await repository.FindUntil<Root>($"{id}1");
            var root2 = await repository.FindUntil<Root>($"{id}2");

            Assert.NotEqual(root.Id, root2.Id);
        }

        [Fact]
        public async void CanProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanProjectRoot)}-Root";
            var command = new CreateRoot(id);
            await bus.CommandAsync(command); 
            
            await bus.IsTrue(new RootInfoQuery(id), c => c?.CreatedAt != default);
        }

        [Fact]
        public void CanCreateProjectionManager()
        {
            var container = CreateContainer();
            var manager = container.GetInstance<IProjectionManager>();

            var stats = manager.GetProjection<Stats>();
            Assert.NotNull(stats);

            var otherStats = manager.GetProjection<Stats>();
            Assert.Equal(stats.Guid, otherStats.Guid);

            var id = $"{nameof(CanCreateProjectionManager)}-Root";
            var rootInfo = manager.GetProjection<RootInfo>(id);
            var otherRootInfo = manager.GetProjection<RootInfo>(id);
            Assert.Equal(otherRootInfo?.Guid, rootInfo?.Guid);
            
            var rootInfo2 = manager.GetProjection<RootInfo>($"{id}2"); 
            
            Assert.NotEqual(rootInfo?.Guid, rootInfo2.Guid);
        }
        
        [Fact]
        public async void CanUpdateRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repo = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanUpdateRoot)}-Root";
            var command = new CreateRoot(id); 
            await await bus.CommandAsync(command);

            var createdAt = (await bus.QueryUntil(new RootInfoQuery(id), r => r.CreatedAt != default)).CreatedAt;
            
            var updateCommand = new UpdateRoot(id);
            await await bus.CommandAsync(updateCommand);

            await bus.IsTrue(new RootInfoQuery(id), r => r.UpdatedAt > createdAt);

            var root = await repo.Find<Root>(id);
            
            var graph = container.GetInstance<IGraph>();
            await graph.Serialise(nameof(CanUpdateRoot));
        }
        
        [Fact]
        public async void CanDeserializeJustMetadata()
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var streamLocator = container.GetInstance<IStreamLocator>();
            var store = container.GetInstance<IEventStore<IAggregate>>();
            var log = container.GetInstance<ILog>();

            var id = $"{nameof(CanUpdateRoot)}-Root";
            var command = new CreateRoot(id); 
            await await bus.CommandAsync(command);

            var updateCommand = new UpdateRoot(id);
            await await bus.CommandAsync(updateCommand);

            var stream = await streamLocator.Find<Root>(id);
            var metadata = await store.ReadStream<IEvent>(stream, 0, 1, SerializationType.Metadata);
            var e = await store.ReadStream<IEvent>(stream, 0, 1);
            Assert.Equal(metadata.MessageId, e.MessageId);
            Assert.Equal(metadata.Version, e.Version);
            Assert.Equal(metadata.Stream, e.Stream);
            
            Assert.Null(metadata.StaticMetadata.CommandId);
            Assert.Null(metadata.StaticMetadata.LocalId);
            Assert.Null(metadata.StaticMetadata.OriginId);

            var copy = e.Copy();
            copy.Version = e.Version + 1;
            await store.AppendToStream(stream, new[] { copy }, false);
            // log.Info(e);
        }
        
        [Theory]
        [InlineData(50000, SerializationType.Metadata)]
        [InlineData(50000, SerializationType.FullMetadata)]
        [InlineData(50000, SerializationType.PayloadAndMetadata)]
        public async void CanDeserializeJustMetadataPerformance(int nLoops, SerializationType serializationType)
        {
            if (Configuration.EventStoreBackendType != EventStoreBackendType.SqlStreamStore)
                return;
            
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var streamLocator = container.GetInstance<IStreamLocator>();
            var store = container.GetInstance<IEventStore<IAggregate>>();
            var log = container.GetInstance<ILog>();

            var id = $"{nameof(CanUpdateRoot)}-Root";
            var command = new CreateRoot(id); 
            await await bus.CommandAsync(command);

            var updateCommand = new UpdateRoot(id);
            await await bus.CommandAsync(updateCommand);

            var stream = await streamLocator.Find<Root>(id);
            
            var stopWatch = Stopwatch.StartNew();
            for (var i = 0; i < nLoops; i++)
                await store.ReadStream<IEvent>(stream, 0, 1, serializationType);
            stopWatch.Stop();
            log.Info($"{stopWatch.ElapsedMilliseconds}ms per {nLoops} iterations");
            
            /*stopWatch = Stopwatch.StartNew();
            for (var i = 0; i < nLoops; i++)
                await store.ReadStream<IEvent>(stream, 0, 1, SerializationType.FullMetadata);
            stopWatch.Stop();
            log.Info($"{stopWatch.ElapsedMilliseconds}ms per {nLoops} iterations of full metadata");
            
            stopWatch = Stopwatch.StartNew();
            for (var i = 0; i < nLoops; i++)
                await store.ReadStream<IEvent>(stream, 0, 1, SerializationType.PayloadAndMetadata);
            stopWatch.Stop();
            log.Info($"{stopWatch.ElapsedMilliseconds}ms per {nLoops} iterations of full metadata and payload");*/
        }


        [Theory]
        [InlineData(1000)]
        public async void CanCreateMultipleRoots(int numRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var i = 0;
            var stopWatch = Stopwatch.StartNew();
            var id = $"{nameof(CanCreateMultipleRoots)}-Root";
            while ( i < numRoots )
            {
                await await bus.CommandAsync(new CreateRoot($"{id}{i}"));
                i++;
            }
           
            await repository.FindUntil<Root>($"{id}{numRoots - 1}");
            log.Info($"No threading : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
            
            stopWatch = Stopwatch.StartNew();
            i = 0;
            while ( i < numRoots )
            {
                await bus.CommandAsync(new CreateRoot($"{id}Thread{i}"));
                i++;
            }
            
            await repository.FindUntil<Root>($"{id}Thread{numRoots - 1}");
            log.Info($"Threading : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
        }

        [Fact]
        public async void CanRecordRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanRecordRoot)}-Root";
            var record = new CreateRecord(id);
            await await bus.CommandAsync(record);

            var time = (DateTime.UtcNow.ToInstant() + Duration.FromMinutes(10)).ToTime(); // (DateTimeOffset)new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc); 
            
            var command = new AddRecord(id, 1) { Timestamp = time };
            await await bus.CommandAsync(command);

            await bus.Equal(new LastRecordQuery(id), c => c.TimeStamp, time);
        }
        
        [Fact]
        public async void CanHistoricalProjectRoot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var timeline = container.GetInstance<ITimeline>();

            var id = $"{nameof(CanHistoricalProjectRoot)}-Root";
            var command = new CreateRoot($"{id}Historical");
            await await bus.CommandAsync(command);

            await await bus.CommandAsync(new CreateRoot($"{id}Temp"));

            var statsQuery = new StatsQuery();
            var now = timeline.Now;
            
            var historicalQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, Time.Default);
            await bus.Equal(historicalQuery, s => s.NumberOfRoots, 0);
            
            var liveQuery = new HistoricalQuery<StatsQuery, Stats>(statsQuery, DateTimeOffset.UtcNow.ToInstant().ToTime());
            await bus.Equal(liveQuery, s => s.NumberOfRoots, 2);

            await await bus.CommandAsync(new UpdateRoot($"{id}Historical"));            
            var historicalInfo = new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery($"{id}Historical"), now);
            await bus.IsTrue(historicalInfo, i => i.CreatedAt == i.UpdatedAt);
            await bus.IsTrue(new RootInfoQuery($"{id}Historical"), i => i.UpdatedAt > i.CreatedAt);

            historicalInfo =
                new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery($"{id}Historical"), DateTimeOffset.UtcNow.ToInstant().ToTime());
            await bus.IsTrue(historicalInfo, i => i.UpdatedAt > i.CreatedAt);
            
            historicalInfo =
                new HistoricalQuery<RootInfoQuery, RootInfo>(new RootInfoQuery($"{id}Temp"), DateTimeOffset.UtcNow.ToInstant().ToTime());
            await bus.IsTrue(historicalInfo, i => i.UpdatedAt == i.CreatedAt);
        }

        [Theory]
        [InlineData(1000)]
        public async void CanProjectALotOfRoots(int numRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var log = container.GetInstance<ILog>();

            var id = $"{nameof(CanProjectALotOfRoots)}-Root";
            var rootId = numRoots; 
            var stopWatch = Stopwatch.StartNew();
            var tasks = new List<Task>();
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                var task = await bus.CommandAsync(command);
                tasks.Add(task);
                rootId--;
            }

            await Task.WhenAll(tasks);
            // await bus.IsTrue(new StatsQuery(), s => s?.NumberOfRoots == numRoots, TimeSpan.FromMilliseconds(numRoots));
            await bus.Equal(new StatsQuery(), s => s?.NumberOfRoots, numRoots);
            await bus.Equal(new RootInfoQuery($"{id}1"), r => r.RootId, $"{id}1");
            log.Info($"{Configuration.ThreadsPerInstance} threads : {stopWatch.ElapsedMilliseconds}ms per {numRoots}");
        }

        [Fact]
        public async void CanUseSaga()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanUseSaga)}";
            var command = new CreateRoot(id);
            await bus.CommandAsync(command);
            await bus.CommandAsync(new UpdateRoot(id));

            await bus.IsTrue(new RootInfoQuery($"{id}Copy"), r => r.UpdatedAt >= r.CreatedAt);
            
            await bus.IsTrue(new RootInfoQuery($"{id}Copy"), r => r.CreatedAt != default);
            await bus.IsTrue(new RootInfoQuery($"{id}Copy"), r => r.UpdatedAt == r.CreatedAt);

            var graph = container.GetInstance<IGraph>();
            await graph.Serialise();
        }

        [Theory]
        [InlineData(10)]
        public async void CanParallelizeSagas(int numRoots)
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanParallelizeSagas)}";
            var rootId = numRoots; 
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }

            await bus.QueryUntil(new RootInfoQuery($"{id}1"));  
            
            rootId = numRoots;
            while (rootId > 0)
            {
                var updateCommand = new UpdateRoot($"{id}{rootId}");
                await bus.CommandAsync(updateCommand);
                rootId--;
            }

            await bus.IsTrue(new StatsQuery(), s => s?.NumberOfRoots == 2 * numRoots);

            /*var remote = container.GetInstance<IRemote>();
            await remote.Push(BranchManager.Master);*/
        }
        
        [Theory]
        [InlineData(10)]         
        public async void CanRebuildProjection(int numberOfRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var messageQueue = container.GetInstance<IMessageQueue>();

            var id = $"{nameof(CanRebuildProjection)}-Root";
            var rootId = numberOfRoots;
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }
            
            var query = new RootInfoQuery($"{id}1");
            await bus.QueryUntil(query, c => c.CreatedAt != default);
            messageQueue.Alert(new InvalidateProjections());

            var statsQuery = new StatsQuery();
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
            
            var newCommand = new CreateRoot($"{id}Other");
            await bus.CommandAsync(newCommand);
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots + 1);
        }
        
        [Theory]
        [InlineData(10)]         
        public async void CanCancelProjection(int numberOfRoots)
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var messageQueue = container.GetInstance<IMessageQueue>();
            var log = container.GetInstance<ILog>();

            var id = $"{nameof(CanCancelProjection)}-Root";
            var rootId = numberOfRoots;
            while (rootId > 0)
            {
                var command = new CreateRoot($"{id}{rootId}");
                await bus.CommandAsync(command);
                rootId--;
            }
            
            log.Info($"{numberOfRoots} roots submitted");
            
            var statsQuery = new StatsQuery();
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
            
            messageQueue.Alert(new ImmediateInvalidateProjections());
            messageQueue.Alert(new ImmediateInvalidateProjections());
            messageQueue.Alert(new ImmediateInvalidateProjections());
            
            Thread.Sleep(50);
            
            await bus.IsTrue(statsQuery, s => s?.NumberOfRoots == numberOfRoots);
        }

        [Fact]
        public async void CanRequestJson()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var queue = container.GetInstance<IMessageQueue>();

            const string url = "https://api.coingecko.com/api/v3/coins/bitcoin/history?date=30-12-2017&localization=false";
            await await bus.CommandAsync(new RequestJson(nameof(CanRequestJson), url));
            await await bus.CommandAsync(new RequestJson(nameof(CanRequestJson), url));

            var res = await queue.Alerts.OfType<JsonRequestCompleted>().FirstAsync();
            Assert.NotNull(res.JsonData); 
            Assert.Contains("13620.3618741461", res.JsonData);
        }

        [Fact]
        public async void CanDeserializeRequestedJson()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var queue = container.GetInstance<IMessageQueue>();

            var connector = container.GetInstance<IJSonConnector>();
            const string url = "https://api.coingecko.com/api/v3/coins/bitcoin/history?date=30-12-2017&localization=false";
            await connector.SetAsync(
                url,
                @"{""id"":""bitcoin"",""symbol"":""btc"",""name"":""Bitcoin"",""image"":{""thumb"":""https://assets.coingecko.com/coins/images/2/thumb/bitcoin.png?1696501400"",""small"":""https://assets.coingecko.com/coins/images/1/small/bitcoin.png?1696501400""},""market_data"":{""current_price"":{""aed"":50024.57906376443,""ars"":253468.12429692186,""aud"":17446.3215245937,""bch"":5.76928286478153,""bdt"":1126110.803183989,""bhd"":5132.860612995706,""bmd"":13620.3618741461,""brl"":45117.7211153463,""btc"":1.0,""cad"":17128.871750393,""chf"":13262.4868659029,""clp"":8362902.190725706,""cny"":88573.2132675718,""czk"":289914.5782287119,""dkk"":84525.1736167662,""eth"":18.483094024188404,""eur"":11345.8976447824,""gbp"":10079.0677868681,""hkd"":106417.930376984,""huf"":3526720.3000726495,""idr"":184652192.175199,""ils"":47387.96303252911,""inr"":869671.001953725,""jpy"":1535062.45448282,""krw"":14537693.2463698,""kwd"":4104.645874754543,""lkr"":2087919.548829924,""ltc"":60.96840666846534,""mmk"":18414729.253845528,""mxn"":267888.750532982,""myr"":55317.8739192755,""ngn"":4884546.501733771,""nok"":111755.75019546246,""nzd"":19178.1505368914,""php"":680527.760679833,""pkr"":1505414.7676248574,""pln"":47450.61669715,""rub"":785377.30638701,""sar"":51079.0811004227,""sek"":111446.704184538,""sgd"":18213.1478981081,""thb"":442954.59869004245,""try"":51700.07425935065,""twd"":404053.46952093,""uah"":382908.08925747185,""usd"":13620.3618741461,""vef"":140859.73944813784,""vnd"":309201434.91677517,""xag"":804.154745877564,""xau"":10.4549897745945,""xdr"":9563.95932114975,""zar"":168771.061713303,""bits"":1000000.0,""link"":22041.447552365687,""sats"":100000000.0},""market_cap"":{""aed"":839030999274.6053,""ars"":4251262431254.5815,""aud"":292616246981.057,""bch"":96764575.68919012,""bdt"":18887552682553.043,""bhd"":86090263023.8938,""bmd"":228445816988.881,""brl"":756731337692.006,""btc"":16772375.0,""cad"":287291860324.498,""chf"":222443403147.498,""clp"":140265731631172.94,""cny"":1485583147878.69,""czk"":4862556024018.788,""dkk"":1417687908840.51,""eth"":310005384.13394696,""eur"":190297650009.907,""gbp"":169049904571.772,""hkd"":1784881435006.67,""huf"":59151475392930.96,""idr"":3097055811734500,""ils"":794808686467.7148,""inr"":14586448171393.6,""jpy"":25746643135006.3,""krw"":243831642763082.0,""kwd"":68844659853.58617,""lkr"":35019369642806.27,""ltc"":1022584979.7960014,""mmk"":308858744568967.1,""mxn"":4493130582220.62,""myr"":927812125576.808,""ngn"":81925445632016.88,""nok"":1874409350684.6182,""nzd"":321663132611.194,""php"":11414066800032.4,""pkr"":25249381013141.95,""pln"":795859537225.861,""rub"":13172642699212.8,""sar"":856717502871.7015,""sek"":1869225915097.14,""sgd"":305477746477.531,""thb"":7429400637203.895,""try"":867133033005.6757,""twd"":6776936310856.11,""uah"":6422278063559.784,""usd"":228445816988.881,""vef"":2362552372426.4595,""vnd"":5186042416962243,""xag"":13487584955.8882,""xau"":175355009.120664,""xdr"":160410312219.069,""zar"":2830691536203.66,""bits"":16772375000000.0,""link"":369687423891.10944,""sats"":1677237500000000},""total_volume"":{""aed"":13223772038.888288,""ars"":67003156399.47071,""aud"":4611856472.88116,""bch"":1525083.9259334763,""bdt"":297682315984.16693,""bhd"":1356848571.721612,""bmd"":3600481281.03768,""brl"":11926666253.0629,""btc"":264345.493482963,""cad"":4527940055.66402,""chf"":3505878635.37842,""clp"":2210695506557.1357,""cny"":23413929770.588,""czk"":76637612249.77382,""dkk"":22343848731.4572,""eth"":4885922.610916088,""eur"":2999236911.91719,""gbp"":2664356147.96788,""hkd"":28131100320.9394,""huf"":932272618099.0865,""idr"":48811974863263.9,""ils"":12526794472.986298,""inr"":229893610179.28,""jpy"":405786842057.429,""krw"":3842973695315.56,""kwd"":1085044639.3347962,""lkr"":551932123488.1709,""ltc"":16116723.547645444,""mmk"":4867850691962.943,""mxn"":70815183958.1755,""myr"":14623030679.6192,""ngn"":1291207855441.2922,""nok"":29542128934.978218,""nzd"":5069657667.76511,""php"":179894446725.766,""pkr"":397949609644.3324,""pln"":12543356686.879,""rub"":207610951627.194,""sar"":13502524900.147509,""sek"":29460434014.7115,""sgd"":4814563569.00357,""thb"":117093051981.26692,""try"":13666681643.19386,""twd"":106809713794.014,""uah"":101220027813.38469,""usd"":3600481281.03768,""vef"":37235637336.29954,""vnd"":81736005898715.08,""xag"":212574683.135671,""xau"":2763729.43132451,""xdr"":2528189546.40031,""zar"":44613869594.2467,""bits"":264345493482.963,""link"":5826557330.308955,""sats"":26434549348296.3}},""community_data"":{""facebook_likes"":null,""twitter_followers"":603664,""reddit_average_posts_48h"":2.042,""reddit_average_comments_48h"":445.896,""reddit_subscribers"":612412,""reddit_accounts_active_48h"":""14074.0""},""developer_data"":{""forks"":13660,""stars"":23665,""subscribers"":2513,""total_issues"":3591,""closed_issues"":3022,""pull_requests_merged"":5038,""pull_request_contributors"":450,""code_additions_deletions_4_weeks"":{""additions"":null,""deletions"":null},""commit_count_4_weeks"":147},""public_interest_stats"":{""alexa_rank"":null,""bing_matches"":null}}");
            await bus.CommandAsync(new RequestJson<TestJson>(nameof(CanDeserializeRequestedJson), url));

            var res = await queue.Alerts.OfType<JsonRequestCompleted<TestJson>>().FirstAsync().Timeout(Configuration.Timeout);
            Assert.NotNull(res.Data);
            Assert.Equal("btc", res.Data.Symbol);
        }

        [Fact]
        public async void CanFailRequestingJson()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var queue = container.GetInstance<IMessageQueue>();

            const string url = "http://localhost";
            await bus.CommandAsync(new RequestJson<TestJson>(nameof(CanDeserializeRequestedJson), url));

            var res = await queue.Alerts.OfType<JsonRequestCompleted<TestJson>>().FirstAsync();
            Assert.Null(res.Data);
        }

        [Fact]
        public async void CanCreateSnapshot()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var id = $"{nameof(CanCreateSnapshot)}-Root";
            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new UpdateRoot(id));
            await await bus.CommandAsync(new CreateSnapshot<Root>(id));
            await await bus.CommandAsync(new UpdateRoot(id));

            var root = await repository.Find<Root>(id);
            Assert.Equal(2, root.SnapshotVersion);

            await bus.QueryUntil(new RootInfoQuery(id), r => r != null && r.UpdatedAt > r.CreatedAt);
        }

        [Fact]
        public async void CanCreateSagaSnapshot()
        {
            var container = CreateContainer(new List<Action<Container>> { Config.RegisterSagas });
            var bus = container.GetInstance<IBus>();
            var repository = container.GetInstance<IEsRepository<ISaga>>();
            var manager = container.GetInstance<IBranchManager>();
            var id = $"{nameof(CanCreateSagaSnapshot)}-Root";
            
            await await bus.CommandAsync(new CreateRoot(id));
            await await bus.CommandAsync(new UpdateRoot(id));

            await manager.Ready;
            
            var saga = await repository.Find<TestSaga>(id);
            Assert.Equal(0, saga.SnapshotVersion);

            await await bus.CommandAsync(new CreateSnapshot<Root>(id));

            await manager.Ready;
            
            saga = await repository.Find<TestSaga>(id);
            Assert.Equal(2, saga.SnapshotVersion);
            Assert.Equal(TestSaga.State.Complete, saga.CurrentState);
        }

        [Fact]
        public async void CanHaveListsInEvents()
        {
            var container = CreateContainer();
            var bus = container.GetInstance<IBus>();

            var id = $"{nameof(CanHaveListsInEvents)}-Root"; 
            await await bus.CommandAsync(new CreateRoot(id));

            var lst = new List<string>()
            {
                "a",
                "b",
                "c",
            };

            await await bus.CommandAsync(new AddRootDetails(id, lst.ToArray()));
            var repository = container.GetInstance<IEsRepository<IAggregate>>();

            var root = await repository.Find<Root>(id);
            
            Assert.Equal(3, root.Details.Count);
        }
    }
}