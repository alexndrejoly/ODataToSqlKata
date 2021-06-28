using Microsoft.OData.Edm;
using System;

namespace ODataToSqlKata
{
    public static class EdmModelHelper
    {
        private const string DefaultNamespace = "ODataToSqlConverter";

        public static (IEdmModel, IEdmEntityType, IEdmEntitySet) BuildTableModel(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentNullException(nameof(tableName));
            }

            var model = new EdmModel();
            var entityType = new EdmEntityType(DefaultNamespace, tableName, null, false, true);
            model.AddElement(entityType);

            var defaultContainer = new EdmEntityContainer(DefaultNamespace, "DefaultContainer");
            model.AddElement(defaultContainer);
            var entitySet = defaultContainer.AddEntitySet(tableName, entityType);

            return (model, entityType, entitySet);
        }
    }
}
