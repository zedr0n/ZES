using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Tests.Domain.Commands
{
    public class UpdateRootHandler : ICommandHandler<UpdateRoot>
    {
        private readonly IEsRepository<IAggregate> _repository;

        public UpdateRootHandler(IEsRepository<IAggregate> repository)
        {
            _repository = repository;
        }

        public async Task Handle(UpdateRoot command)
        {
            var root = await _repository.Find<Root>(command.Target);
            if (root == null)
                throw new ArgumentNullException();
            
            root.Update();
            await _repository.Save(root, command.MessageId);
        }
    }
}