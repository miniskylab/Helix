namespace Helix.Persistence.Abstractions
{
    public interface ISQLitePersistence<in TDto> where TDto : class
    {
        void Save(TDto dto);
    }
}