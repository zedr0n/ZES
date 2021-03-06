/// <filename>
///     UpdateRootHandler.cs
/// </filename>

// <auto-generated/>
 namespace ZES.Tests.Domain.Commands
{
  public class UpdateRootHandler : ZES.Infrastructure.Domain.CommandHandlerBase<UpdateRoot, Root>
  {
    public UpdateRootHandler(ZES.Interfaces.Domain.IEsRepository<ZES.Interfaces.Domain.IAggregate> repository) : base(repository) 
    {
    }  
    protected override void Act (Root root, UpdateRoot command)
    {
      root.Update();
    }
  }
}

