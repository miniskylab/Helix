using System.Threading.Tasks.Dataflow;

namespace Helix.Bot.Abstractions
{
    public interface IRendererPoolBlock : IPropagatorBlock<IHtmlRenderer, IHtmlRenderer>, IReceivableSourceBlock<IHtmlRenderer>
    {
        BufferBlock<Event> Events { get; }
    }
}