using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;

namespace AccsoCourier.Infrastructure.Data
{
    public class SqlConnectionFactory(string connectionString)
    {
        public SqlConnection Create() => new(connectionString);
    }
}
