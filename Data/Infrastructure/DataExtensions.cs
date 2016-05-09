﻿using System.Collections.Generic;
using System.Linq;
using Fr8Data.Control;
using Fr8Data.DataTransferObjects;

namespace Data.Infrastructure
{
    public static class DataExtensions
    {
        public static IEnumerable<ListItem> ToListItems(this IEnumerable<FieldDTO> fields)
        {
            return fields.Select(x => new ListItem() { Key = x.Key, Value = x.Value });
        }
    }
}
