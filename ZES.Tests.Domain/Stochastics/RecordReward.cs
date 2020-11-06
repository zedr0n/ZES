using System;
using ZES.Infrastructure.Stochastics;
using ZES.Interfaces.Domain;
using ZES.Interfaces.Pipes;
using ZES.Tests.Domain.Queries;

namespace ZES.Tests.Domain.Stochastics
{
    public class RecordReward : BranchReward<TotalRecord>
    {
        public RecordReward(IBus bus) 
            : base(bus)
        {
        }

        protected override IQuery<TotalRecord> ValueQuery => new TotalRecordQuery();
        protected override Func<TotalRecord, double> Value => r => r.Total;
    }
}