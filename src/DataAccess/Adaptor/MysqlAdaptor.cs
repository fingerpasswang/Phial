using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace DataAccess
{
    // interlayer between mysqlClient and dataAccess
    public class MysqlAdaptor
    {
        //private readonly ORMHelper helper = new ORMHelper();
        // todo connectionPool
        internal MySqlConnection Handle { get; private set; }

        public MysqlAdaptor(string ip, string db, string user, string pass)
        {
            var connectStr =
                string.Format("Server={0};Database={1};User ID={2};Password={3};Pooling=false;", ip, db, user, pass);

            Handle = new MySqlConnection(connectStr);
            Handle.Open();
        }

        internal static byte[] ReadBlob(MySqlDataReader reader, string column)
        {
            long len = reader.GetBytes(reader.GetOrdinal(column), 0, null, 0, 0);
            if (len != 0)
            {
                var buffer = new byte[len];
                reader.GetBytes(reader.GetOrdinal(column), 0, buffer, 0, (int)len);

                return buffer;
            }
            return null;
        }
        internal static T ReadBlob<T>(ORMHelper helper, MySqlDataReader reader, string column)
        {
            var data = ReadBlob(reader, column);

            return data != null ? helper.BytesToObject<T>(data) : default(T);
        }

        // todo to ensure thread-safe
        // todo one handle can associate one dataReader at most
        public async Task<ILoadDataContext> Query(string sql, object key)
        {
            var cmd = new MySqlCommand(sql, Handle);

            cmd.Parameters.AddWithValue("1", key);

            var ret = await Task.Factory.FromAsync(cmd.BeginExecuteNonQuery(), (ar => cmd.EndExecuteNonQuery(ar)), TaskCreationOptions.None);
            var reader = await Task.Factory.FromAsync(cmd.BeginExecuteReader(), (ar => cmd.EndExecuteReader(ar)), TaskCreationOptions.None);

            if (!reader.Read())
            {
                cmd.Dispose();
                reader.Dispose();
                return null;
            }

            return new MysqlLoadDataContext()
            {
                Reader = reader,
                MysqlAdaptor = this,
            };
        }

        public IUpdateDataContext Update(string sql)
        {
            return new MysqlUpdateDataContext()
            {
                MySqlCommand = new MySqlCommand(sql, Handle),
            };
        }
    }
}
