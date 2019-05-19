using SqlStreamStore.Streams;
using Xunit;
using Xunit.Abstractions;
using ZES.Infrastructure.Streams;
using ZES.Tests.Domain;

namespace ZES.Tests
{
    public class FuncTests : Test
    {
        private readonly string _timeline = "master";
        private readonly string _branchTimeline = "branch";
        private readonly string _type = nameof(Root);
        private readonly string _id = nameof(Root);
        
        public FuncTests(ITestOutputHelper outputHelper) 
            : base(outputHelper)
        {
        }

        [Fact]
        public void EmptyStream()
        {
            var stream = new Stream(_id, _type, ExpectedVersion.EmptyStream, _timeline);
            Assert.Equal(0, stream.Count(0));
            Assert.Equal(0, stream.Count(0, 1));
        }

        [Fact]
        public void SingleStream()
        {
            var version = 1;
            var stream = new Stream(_id, _type, version, _timeline);
            Assert.Equal(version + 1, stream.Count(0));
            Assert.Equal(1, stream.Count(0, 1));
            Assert.Equal(1, stream.Count(1, 1));
        }

        [Fact]
        public void ClonedStream()
        {
            var version = 1;
            var stream = new Stream(_id, _type, version, _timeline);
            var otherStream = stream.Branch(_branchTimeline, version);
            Assert.Equal(0, otherStream.Count(0));
            Assert.Equal(0, otherStream.Count(0, 1));
            Assert.Equal(0, otherStream.Count(version));
            Assert.Equal(0, otherStream.Count(version, 1));
        }

        [Fact]
        public void ContinuedStream()
        {
            var version = 1;
            var count = 2;
            var otherVersion = version + count;
            var stream = new Stream(_id, _type, version, _timeline);
            var otherStream = stream.Branch(_branchTimeline, version);
            otherStream.Version = otherVersion;
            
            Assert.Equal(count, otherStream.Count(0)); 
            Assert.Equal(0, otherStream.Count(0, 1)); 
            Assert.Equal(count, otherStream.Count(version)); 
            Assert.Equal(count, otherStream.Count(version + 1)); 
            Assert.Equal(count - 1, otherStream.Count(version + 2)); 
            Assert.Equal(0, otherStream.Count(version, 1)); 
            Assert.Equal(1, otherStream.Count(version + 1, 1)); 
            Assert.Equal(1, otherStream.Count(version + 2, 1)); 
        }
    }
}