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
using ZES.TestBase;
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
            
            var connector = container.GetInstance<IJSonConnector>();
            const string url = "https://api.coingecko.com/api/v3/coins/bitcoin/history?date=30-12-2023&localization=false";
            await connector.SetAsync(
                url,
                @"{""id"":""bitcoin"",""symbol"":""btc"",""name"":""Bitcoin"",""image"":{""thumb"":""https://coin-images.coingecko.com/coins/images/0/thumb/bitcoin.png?1696501400"",""small"":""https://coin-images.coingecko.com/coins/images/1/small/bitcoin.png?1696501400""},""market_data"":{""current_price"":{""aed"":154530.09108142683,""ars"":33947900.26188303,""aud"":61738.405695047535,""bch"":165.38167494630605,""bdt"":4617857.437514718,""bhd"":15859.429741917913,""bmd"":42074.70715618848,""bnb"":134.15687497173963,""brl"":204167.47440069792,""btc"":1.0,""cad"":55797.37289517942,""chf"":35380.41087410315,""clp"":37070945.97434911,""cny"":297872.0967829519,""czk"":941726.6142466004,""dkk"":284202.0244279068,""dot"":5078.184550422312,""eos"":49611.91197615977,""eth"":18.29654321540394,""eur"":38057.70863986569,""gbp"":33025.65781339986,""gel"":113180.96225014742,""hkd"":328622.3965080529,""huf"":14607917.577557135,""idr"":647533950.6044563,""ils"":151504.70926336164,""inr"":3501412.9510954972,""jpy"":5933586.69083973,""krw"":54466970.65490067,""kwd"":12928.716014953645,""lkr"":13628686.368770933,""ltc"":573.6621797872364,""mmk"":88364275.74483082,""mxn"":714058.2700891062,""myr"":193333.27938268633,""ngn"":37725865.42452484,""nok"":429848.16731742636,""nzd"":66531.8087825235,""php"":2330938.6923034303,""pkr"":11705926.359806487,""pln"":165640.54862662574,""rub"":3755167.3612415865,""sar"":157780.1518357064,""sek"":423808.00650749775,""sgd"":55568.065741178136,""thb"":1438863.9771500682,""try"":1240206.6063985475,""twd"":1291001.3807622658,""uah"":1599892.6750505993,""usd"":42074.70715618848,""vef"":4212.940427549151,""vnd"":1021106970.8227047,""xag"":1768.3279940253694,""xau"":20.39613504103393,""xdr"":31351.757663898043,""xlm"":324963.64104682615,""xrp"":67529.86361098202,""yfi"":5.116942760598554,""zar"":769994.6998914372,""bits"":1000195.713931052,""link"":2709.6608365050256,""sats"":100019571.3931052},""market_cap"":{""aed"":3022434823129.84,""ars"":663982757051427.4,""aud"":1207533794818.6636,""bch"":3239927812.6139565,""bdt"":90320099015790.61,""bhd"":310192612917.6729,""bmd"":822933961870.5416,""bnb"":2629923038.0492373,""brl"":3993286227042.8438,""btc"":19584275.0,""cad"":1091498460326.9937,""chf"":692169755329.3134,""clp"":725066019537891.1,""cny"":5826043276458.686,""czk"":18419113668076.95,""dkk"":5558672032246.961,""dot"":99489102293.36188,""eos"":971966018054.8785,""eth"":358260658.6305346,""eur"":744365987728.8765,""gbp"":645995753662.7186,""gel"":2213692357431.7495,""hkd"":6427484562491.774,""huf"":285714442221834.44,""idr"":12665035966583838,""ils"":2963261756601.5366,""inr"":68483700226206.57,""jpy"":116054283764084.62,""krw"":1065312701660273.2,""kwd"":252871147803.5803,""lkr"":266561780855891.12,""ltc"":11241964101.69766,""mmk"":1728305874039080,""mxn"":13966176853697.344,""myr"":3781381554795.1514,""ngn"":737875507571602.2,""nok"":8407346818129.712,""nzd"":1301287369358.7278,""php"":45590539841760.11,""pkr"":228954757091481.3,""pln"":3239742879771.195,""rub"":73446851159342.03,""sar"":3086002357014.532,""sek"":8289208064431.51,""sgd"":1086848883442.4248,""thb"":28142561489813.086,""try"":24257046694416.734,""twd"":25250535365752.875,""uah"":31292101755089.6,""usd"":822933961870.5416,""vef"":82400377602.09746,""vnd"":19971704184972804,""xag"":34586507200.34415,""xau"":398925467.35636365,""xdr"":613205127018.0251,""xlm"":6366989968394.301,""xrp"":1322171541704.1318,""yfi"":100197984.57701135,""zar"":15060230523975.951,""bits"":19587833186725.145,""link"":53027090934.88813,""sats"":1958783318672514.8},""total_volume"":{""aed"":91203312150.08063,""ars"":20035974370796.53,""aud"":36437868164.37399,""bch"":97607892.53714487,""bdt"":2725449072027.6714,""bhd"":9360199758.84335,""bmd"":24832397519.050613,""bnb"":79179085.83047172,""brl"":120499184128.79588,""btc"":590313.2604817993,""cad"":32931483969.88901,""chf"":20881438911.782093,""clp"":21879188925189.88,""cny"":175803441475.87073,""czk"":555804929370.7711,""dkk"":167735395521.93146,""dot"":2997133098.5874844,""eos"":29280838849.3072,""eth"":10798578.648754122,""eur"":22461574030.714294,""gbp"":19491668952.230877,""gel"":66799149326.2464,""hkd"":193952199202.66922,""huf"":8621560094639.218,""idr"":382173081057940.94,""ils"":89417738606.47363,""inr"":2066526047518.001,""jpy"":3501989517686.002,""krw"":32146283560336.594,""kwd"":7630499109.653902,""lkr"":8043620037935.51,""ltc"":338574128.0917383,""mmk"":52152396774457.34,""mxn"":421435584775.31195,""myr"":114104866600.03775,""ngn"":22265720911481.547,""nok"":253695421433.0574,""nzd"":39266923884.12937,""php"":1375714772890.6108,""pkr"":6908811405778.086,""pln"":97760679200.94873,""rub"":2216291329580.8867,""sar"":93121490696.43959,""sek"":250130532110.01724,""sgd"":32796147403.410156,""thb"":849214282675.6979,""try"":731967149325.9987,""twd"":761946110895.6648,""uah"":944253057952.1875,""usd"":24832397519.050613,""vef"":2486467963.582537,""vnd"":602655037260633.8,""xag"":1043663204.3259426,""xau"":12037753.021334978,""xdr"":18503736849.33296,""xlm"":191792809959.6043,""xrp"":39855973598.82108,""yfi"":3020008.107049232,""zar"":454449139819.0017,""bits"":590313260481.7993,""link"":1599235730.4856293,""sats"":59031326048179.93}},""community_data"":{""facebook_likes"":null,""twitter_followers"":null,""reddit_average_posts_48h"":0.0,""reddit_average_comments_48h"":0.0,""reddit_subscribers"":null,""reddit_accounts_active_48h"":null},""developer_data"":{""forks"":36262,""stars"":72871,""subscribers"":3961,""total_issues"":7736,""closed_issues"":7377,""pull_requests_merged"":11204,""pull_request_contributors"":846,""code_additions_deletions_4_weeks"":{""additions"":973,""deletions"":-290},""commit_count_4_weeks"":163},""public_interest_stats"":{""alexa_rank"":null,""bing_matches"":null}}");
            var obs = queue.Alerts.OfType<JsonRequestCompleted>().FirstAsync().Replay();
            obs.Connect();
            
            await await bus.CommandAsync(new RequestJson(nameof(CanRequestJson), url));
            await await bus.CommandAsync(new RequestJson(nameof(CanRequestJson), url));

            //var res = await queue.Alerts.OfType<JsonRequestCompleted>().FirstAsync();
            var res = await obs;
            Assert.NotNull(res.JsonData); 
            Assert.Contains("42074.70715618848", res.JsonData);
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