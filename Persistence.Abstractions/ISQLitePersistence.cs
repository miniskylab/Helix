using System;

namespace Helix.Persistence.Abstractions
{
    public interface ISQLitePersistence<in TDto> : IDisposable where TDto : class
    {
        void Save(TDto dto);
    }
}