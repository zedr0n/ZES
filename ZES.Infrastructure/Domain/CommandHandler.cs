using System;
using System.Threading.Tasks;
using ZES.Interfaces;
using ZES.Interfaces.Domain;

namespace ZES.Infrastructure.Domain
{
    public abstract class CommandHandler<TCommand,T> : ICommandHandler<TCommand> where TCommand : ICommand where T : class,IAggregate
    {
        private readonly IDomainRepository _repository;

        protected CommandHandler(IDomainRepository repository)
        {
            _repository = repository;
        }

        protected virtual T Act(TCommand command)
        {
            return default(T);
        }

        public virtual async Task Handle(TCommand command)
        {
            var aggregate = Act(command);
            await _repository.Save(aggregate);
        }
    }
}