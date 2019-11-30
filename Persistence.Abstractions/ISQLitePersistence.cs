using System;
using System.Collections.Generic;
using Helix.Core;

namespace Helix.Persistence.Abstractions
{
    public interface ISqLitePersistence<TDataTransferObject> : IService where TDataTransferObject : class
    {
        void Delete(params TDataTransferObject[] dataTransferObjects);

        void Insert(params TDataTransferObject[] dataTransferObjects);

        List<TDataTransferObject> Select(Func<TDataTransferObject, bool> whereClause);

        void Update(params TDataTransferObject[] dataTransferObjects);
    }
}