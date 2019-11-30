using System.Threading.Tasks.Dataflow;
using Helix.Core;

namespace Helix.Bot.Abstractions
{
    public interface IRendererPoolBlock : IPropagatorBlock<IHtmlRenderer, IHtmlRenderer>, IReceivableSourceBlock<IHtmlRenderer>, IService
    {
        BufferBlock<Event> Events { get; }

        void Activate();
    }
}