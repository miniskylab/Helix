namespace Helix.Persistence.Abstractions
{
    public interface ISQLitePersistence<TDataTransferObject> where TDataTransferObject : class
    {
        TDataTransferObject GetByPrimaryKey(params object[] primaryKeyValues);

        void Save(params TDataTransferObject[] dataTransferObjects);

        void Update(params TDataTransferObject[] dataTransferObjects);
    }
}