using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Database;
using MySql.Data.MySqlClient;
using SQLite.Net;
using SQLite.Net.Async;
using SQLite.Net.Platform.WinRT;

namespace DrongoDatabase.UWP
{
    public static class Database
    {
        private static string path = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path + @"\", "db.sqlite");
        private static SQLiteAsyncConnection conn = null;
        public const string IdField = "Id";
        private static bool isOnline;
        private static DbConnection dbCon;

        public static bool CheckOnline()
        {
            return isOnline;
        }

        public static void InitMysql()
        {
            dbCon = DbConnection.Instance();
            isOnline = true;
        }

        public static void InitLocal()
        {
            var connectionFactory = new Func<SQLiteConnectionWithLock>(() => new SQLiteConnectionWithLock(new SQLitePlatformWinRT(), new SQLiteConnectionString(path, storeDateTimeAsTicks: false)));
            conn = new SQLiteAsyncConnection(connectionFactory);      
        }

        public static async void TestRemote<T>(T testObject = null) where T : class, new()
        {
            if (dbCon.IsConnect())
            {
                await Insert(testObject);

                return;
                List<T> list = new List<T>();
                PropertyInfo[] props = ObjectHelper.GetReflectionProperties(testObject);
                string query = "";


                // Delete

                int id = Convert.ToInt32(ObjectHelper.GetField(testObject, "Id").GetValue(testObject, null));

                query = "DELETE FROM " + typeof(T).Name.ToLower() + "s WHERE Id=" + id;
                var cmdDelete = new MySqlCommand(query, dbCon.Connection);
                cmdDelete.ExecuteNonQuery();
                dbCon.Close();
                return;


                // Insert
                query = "INSERT INTO " + typeof(T).Name.ToLower() + "s (";
                string queryEnd = "";

                for (int i = 0; i < props.Length; i++)
                {
                    if (props[i].Name == "Id") continue;

                    query = query + props[i].Name;
                    string value = Convert.ToString(testObject.GetType().GetProperty(props[i].Name).GetValue(testObject, null));
                    if (!string.IsNullOrEmpty(value))
                    {
                        queryEnd = queryEnd + "'" + value + "'";
                    }

                    if (i != props.Length - 1)
                    {
                        query = query + ", ";
                        queryEnd = queryEnd + ", ";
                    }
                    else
                    {
                        query = query + ")";
                        queryEnd = queryEnd + ")";
                    }

                }

                query = query + " VALUES (" + queryEnd + ";SELECT LAST_INSERT_ID();";

                var cmd = new MySqlCommand(query, dbCon.Connection);

                var newId = cmd.ExecuteScalar();

                Debug.WriteLine("Inserted ID:" + newId);

                dbCon.Close();

                return;
                // Select
                query = "SELECT * FROM " + typeof(T).Name.ToLower() + "s";


                var reader = cmd.ExecuteReader();



                while (reader.Read())
                {
                    T obj = new T();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {

                        try
                        {
                            PropertyInfo propertyInfo = obj.GetType().GetProperty(props[i].Name);
                            propertyInfo.SetValue(obj, Convert.ChangeType(reader.GetValue(i), propertyInfo.PropertyType), null);
                        }
                        catch
                        {
                            continue;
                        }

                    }
                    list.Add(obj);
                }

                //list = list;
                reader.Close();
                dbCon.Close();
            }
        }

        public static void ReleaseDatabase()
        {
            dbCon?.Close();
        }

        public static async Task<bool> CreateTable<T>()
        {
            if (!DbException.CheckDatabaseConnection(conn)) return false;
            await conn.CreateTablesAsync(typeof(T));
            return true;
        }

        public static async Task<bool> DeleteAll<T>()
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>());

            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return false;
                await conn.DeleteAllAsync(typeof(T));
            }
            else
            {
                if (dbCon.IsConnect())
                {
                    string query = "DELETE FROM " + typeof(T).Name.ToLower() + "s";
                    var cmdDelete = new MySqlCommand(query, dbCon.Connection);
                    cmdDelete.ExecuteNonQuery();
                }
                dbCon.Close();

            }

            return true;
        }

        public static bool CheckDatabaseConnection()
        {
            return DbException.CheckDatabaseConnection(conn);
        }

        public static SQLiteAsyncConnection GetConnectionLocal()
        {
            return conn;
        }


        public static async Task<int> Insert(object item, bool skipId = true)
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.ToString(item));
            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return 0;
                return await conn.InsertAsync(item);
            }
            else
            {
                if (dbCon.IsConnect())
                {
                    PropertyInfo[] props = ObjectHelper.GetReflectionProperties(item, true);
                    string query = "INSERT INTO " + item.GetType().Name.ToLower() + "s (";
                    string queryEnd = "";
                    for (int i = 0; i < props.Length; i++)
                    {
                        if (props[i].Name == "Id" && skipId) continue;
                        query = query + props[i].Name;
                        string value = Convert.ToString(item.GetType().GetProperty(props[i].Name).GetValue(item, null));

                        if (typeof(DateTime).IsAssignableFrom(props[i].PropertyType))
                        {
                            DateTime dateTime = (DateTime)item.GetType().GetProperty(props[i].Name).GetValue(item, null);
                            value = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        }

                        if (typeof(bool).IsAssignableFrom(props[i].PropertyType))
                        {
                            queryEnd = queryEnd + SqlHelper.Escape(value);

                        }
                        else
                        {
                            queryEnd = queryEnd + "'" + SqlHelper.Escape(value) + "'";


                        }


                        if (i != props.Length - 1)
                        {
                            query = query + ", ";
                            queryEnd = queryEnd + ", ";
                        }
                        else
                        {
                            query = query + ")";
                            queryEnd = queryEnd + ")";
                        }
                    }
                    query = query + " VALUES (" + queryEnd + ";SELECT LAST_INSERT_ID();";
                    var cmd = new MySqlCommand(query, dbCon.Connection);
                    var newId = cmd.ExecuteScalar();
                    Debug.WriteLine("Inserted ID:" + newId);
                    return Convert.ToInt32(newId);
                }
                dbCon.Close();
            }
            return 0;
        }

        public static async Task<bool> Delete<T>(object item)
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.ToString(item));
            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return false;
                await conn.DeleteAsync((T)item);
            }
            else
            {
                PropertyInfo field = ObjectHelper.GetField(item, "Id");
                if (field == null)
                {
                    return false;
                }

                int id = Convert.ToInt32(field.GetValue(item, null));

                string query = "DELETE FROM " + typeof(T).Name.ToLower() + "s WHERE Id=" + id;
                var cmdDelete = new MySqlCommand(query, dbCon.Connection);
                cmdDelete.ExecuteNonQuery();
                dbCon.Close();
            }

            return true;
        }

        public static async Task<bool> Update<T>(object item)
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.ToString(item));
            if (!DbException.CheckDatabaseConnection(conn)) return false;
            if (!isOnline)
            {
                await conn.UpdateAsync((T)item);
                return true;
            }
           

            if (dbCon.IsConnect())
            {
                int id = -1;
                PropertyInfo[] props = ObjectHelper.GetReflectionProperties(item, true);
                string query = "UPDATE " + item.GetType().Name.ToLower() + "s SET ";
                for (int i = 0; i < props.Length; i++)
                {
                    if (props[i].Name == "Id")
                    {
                        id = Convert.ToInt32(item.GetType().GetProperty(props[i].Name).GetValue(item, null));
                        continue;
                    }
                    string value = Convert.ToString(item.GetType().GetProperty(props[i].Name).GetValue(item, null));
                    if (typeof(DateTime).IsAssignableFrom(props[i].PropertyType))
                    {
                        DateTime dateTime = (DateTime)item.GetType().GetProperty(props[i].Name).GetValue(item, null);
                        value = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                    query = query + props[i].Name + " = '" + SqlHelper.Escape(value) + "'";

                    if (i != props.Length - 1)
                    {
                        query = query + ", ";
                    }
                }
                query = query + " WHERE Id=" + id;
                var cmd = new MySqlCommand(query, dbCon.Connection);
                cmd.ExecuteNonQuery();
            }
            dbCon.Close();

            return true;
        }
        public static async Task<bool> InsertOrUpdate<T>(object item, int id) where T : class
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.ToString(item) + " - id: " + id);
            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return false;
            }
            if (await Exists<T>(id))
            {
                await Update<T>((T)item);
            }
            else
            {
                await Insert(item);
            }
            return true;
        }

        public static async Task<bool> InsertIfNotExists<T>(object item, int id) where T : class
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.ToString(item) + " - id: " + id);
            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return false;
            }
            if (!await Exists<T>(id))
            {
                await Insert(item);
                return true;
            }
            return false;
        }

        public static async Task<List<T>> GetAll<T>() where T : class, new()
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>());
            if (isOnline)
            {
                if (dbCon.IsConnect())
                {
                    string query = "SELECT * FROM " + typeof(T).Name.ToLower() + "s;";
                    var cmd = new MySqlCommand(query, dbCon.Connection);
                    var reader = cmd.ExecuteReader();

                    List<T> list = new List<T>();
                    PropertyInfo[] props = ObjectHelper.GetReflectionProperties(new T());


                    while (reader.Read())
                    {
                        T obj = new T();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {

                            try
                            {
                                PropertyInfo propertyInfo = obj.GetType().GetProperty(props[i].Name);
                                propertyInfo.SetValue(obj,
                                    Convert.ChangeType(reader.GetValue(i), propertyInfo.PropertyType), null);
                            }
                            catch(Exception e)
                            {
                                Debug.WriteLine(ObjectHelper.GetCaller() + " ERROR : " + e.Message + " - " + props[i].Name);
                            }

                        }
                        list.Add(obj);
                    }

                    reader.Close();
                    dbCon.Close();
                    foreach (var res in list)
                    {
                        Debug.WriteLine(ObjectHelper.GetCaller() + " RESULT: " + ObjectHelper.ToString(res));
                    }
                    return list;
                }
                dbCon.Close();
            }
            else
            {
                if (!DbException.CheckDatabaseConnection(conn)) return null;
                var query = conn.Table<T>();
                return await query.ToListAsync();
            }

            return null;
        }

        public static async Task<List<T>> GetAll<T>(object value, string field = IdField) where T : class, new()
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>() + " where " + field + " = " + value);
            if (isOnline)
            {
                if (dbCon.IsConnect())
                {
                    string query = "SELECT * FROM " + typeof(T).Name.ToLower() + "s WHERE " + field + " = " + value + ";";
                    var cmd = new MySqlCommand(query, dbCon.Connection);
                    var reader = cmd.ExecuteReader();

                    List<T> list = new List<T>();
                    PropertyInfo[] props = ObjectHelper.GetReflectionProperties(new T());


                    while (reader.Read())
                    {
                        T obj = new T();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {

                            try
                            {
                                PropertyInfo propertyInfo = obj.GetType().GetProperty(props[i].Name);
                                propertyInfo.SetValue(obj,
                                    Convert.ChangeType(reader.GetValue(i), propertyInfo.PropertyType), null);
                            }
                            catch
                            {
                                continue;
                            }

                        }
                        list.Add(obj);
                    }

                    reader.Close();
                    dbCon.Close();
                    foreach (var res in list)
                    {
                        Debug.WriteLine(ObjectHelper.GetCaller() + " RESULT: " + ObjectHelper.ToString(res));
                    }
                    return list;
                }
                dbCon.Close();
            }
            else
            {
                if (!DbException.CheckDatabaseConnection(conn)) return null;

                var item = Expression.Parameter(typeof(T), "item");
                var prop = Expression.Property(item, field);
                var soap = Expression.Constant(value);
                var equal = Expression.Equal(prop, soap);
                var lambda = Expression.Lambda<Func<T, bool>>(equal, item);

                var query = await conn.Table<T>().Where(lambda).ToListAsync();
                return query;
            }
            return null;
        }
        public static async Task<List<T>> GetAllContains<T>(object value, string field = IdField) where T : class, new()
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>() + " where " + field + " contains " + value);
            if (isOnline)
            {
                if (dbCon.IsConnect())
                {
                    string query = "SELECT * FROM " + typeof(T).Name.ToLower() + "s WHERE " + field + @" LIKE ""%" + value + @"%"";";
                    var cmd = new MySqlCommand(query, dbCon.Connection);
                    var reader = cmd.ExecuteReader();

                    List<T> list = new List<T>();
                    PropertyInfo[] props = ObjectHelper.GetReflectionProperties(new T());


                    while (reader.Read())
                    {
                        T obj = new T();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            try
                            {
                                PropertyInfo propertyInfo = obj.GetType().GetProperty(props[i].Name);
                                propertyInfo.SetValue(obj,
                                    Convert.ChangeType(reader.GetValue(i), propertyInfo.PropertyType), null);
                            }
                            catch
                            {
                                continue;
                            }

                        }
                        list.Add(obj);
                    }

                    reader.Close();
                    dbCon.Close();
                    foreach (var res in list)
                    {
                        Debug.WriteLine(ObjectHelper.GetCaller() + " RESULT: " + ObjectHelper.ToString(res));
                    }
                    return list;
                }

                dbCon.Close();
            }
            else
            {
                if (!DbException.CheckDatabaseConnection(conn)) return null;

                var item = Expression.Parameter(typeof(T), "item");
                var prop = Expression.Property(item, field);
                MethodInfo method = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                var soap = Expression.Constant(value);
                var containsMethodExp = Expression.Call(prop, method, soap);
                var lambda = Expression.Lambda<Func<T, bool>>(containsMethodExp, item);

                var query = await conn.Table<T>().Where(lambda).ToListAsync();
                return query;
            }
            return null;
        }

        public static async Task<T> Get<T>(object value, string field = IdField) where T : class, new()
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>() + " where " + field + " = " + value);
            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return null;

                var item = Expression.Parameter(typeof(T), "item");
                var prop = Expression.Property(item, field);
                var soap = Expression.Constant(value);
                var equal = Expression.Equal(prop, soap);
                var lambda = Expression.Lambda<Func<T, bool>>(equal, item);

                var query = await conn.Table<T>().Where(lambda).FirstOrDefaultAsync();
                return query;
            }

            if (dbCon.IsConnect())
            {
                string query = "SELECT * FROM " + typeof(T).Name.ToLower() + "s WHERE " + field + " = '" + SqlHelper.Escape(value.ToString()) + "' LIMIT 1;";
                var cmd = new MySqlCommand(query, dbCon.Connection);
                var reader = cmd.ExecuteReader();

                PropertyInfo[] props = ObjectHelper.GetReflectionProperties(new T());


                while (reader.Read())
                {
                    T obj = new T();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        try
                        {
                            PropertyInfo propertyInfo = obj.GetType().GetProperty(props[i].Name);
                            propertyInfo.SetValue(obj,
                                Convert.ChangeType(reader.GetValue(i), propertyInfo.PropertyType), null);
                        }
                        catch
                        {
                            continue;
                        }

                    }
                    reader.Close();
                    dbCon.Close();

                    Debug.WriteLine(ObjectHelper.GetCaller() + " RESULT: " + ObjectHelper.ToString(obj));
                    return obj;
                }
                reader.Close();

            }
            dbCon.Close();

            return null;
        }



        public static async Task<bool> Exists<T>(object value, string field = IdField) where T : class
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>() + " where " + field + " = " + value);
            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return false;
                var item = Expression.Parameter(typeof(T), "item");
                var prop = Expression.Property(item, field);
                var soap = Expression.Constant(value);
                var equal = Expression.Equal(prop, soap);
                var lambda = Expression.Lambda<Func<T, bool>>(equal, item);

                var queryOffline = await conn.Table<T>().Where(lambda).CountAsync();
                return queryOffline > 0;
            }

            if (dbCon.IsConnect())
            {
                string query = "SELECT EXISTS(SELECT 1 FROM " + typeof(T).Name.ToLower() + "s WHERE " + field + " ='" + SqlHelper.Escape(value.ToString()) + "' LIMIT 1)";
                var cmd = new MySqlCommand(query, dbCon.Connection);
                var result = cmd.ExecuteScalar();
                if (result == null)
                {
                    return false;
                }
                dbCon.Close();
                return Convert.ToInt32(result) > 0;
            }
            dbCon.Close();

            return false;

        }

        public static async Task<int> Count<T>()
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>());
            if (!isOnline)
            {
                if (!DbException.CheckDatabaseConnection(conn)) return 0;
                int count = await conn.ExecuteScalarAsync<int>("select count(*) from " + typeof(T).Name);
                return count;
            }
            else
            {
                if (dbCon.IsConnect())
                {
                    string query = "select count(*) from " + typeof(T).Name.ToLower() + "s";
                    var cmd = new MySqlCommand(query, dbCon.Connection);
                    var count = cmd.ExecuteScalar();
                    dbCon.Close();
                    return Convert.ToInt32(count);
                }
                dbCon.Close();
                return 0;
            }
        }

        public static async Task<T> CustomSqlResult<T>(string sql) where T : class, new()
        {
            return await Task.Factory.StartNew(() => CustomSqlResultTask<T>(sql));
        }

        private static T CustomSqlResultTask<T>(string sql) where T : class, new()
        {
            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>() + "SQL: " + sql);
            if (dbCon.IsConnect())
            {
                string query = sql;
                var cmd = new MySqlCommand(query, dbCon.Connection);
                var reader = cmd.ExecuteReader();
                PropertyInfo[] props = ObjectHelper.GetReflectionProperties(new T());
                while (reader.Read())
                {
                    T obj = new T();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        try
                        {
                            PropertyInfo propertyInfo = obj.GetType().GetProperty(props[i].Name);
                            propertyInfo.SetValue(obj,
                                Convert.ChangeType(reader.GetValue(i), propertyInfo.PropertyType), null);
                        }
                        catch
                        {
                        }

                    }
                    reader.Close();
                    dbCon.Close();
                    return obj;
                }

                reader.Close();
                dbCon.Close();
            }

            return null;
        }

        public static async Task<List<T>> CustomSqlResultList<T>(string sql) where T : class, new()
        {
           return await Task.Factory.StartNew(() => CustomSqlResultListTask<T>(sql));
        }

        private static List<T> CustomSqlResultListTask<T>(string sql) where T : class, new()
        {
           

            Debug.WriteLine(ObjectHelper.GetCaller() + ": " + ObjectHelper.GetType<T>() + "SQL: " + sql);
            if (dbCon.IsConnect())
            {
                string query = sql;
                var cmd = new MySqlCommand(query, dbCon.Connection);
                var reader =  cmd.ExecuteReader();
                

                //MySqlDataReader reader = null;
                //IAsyncResult result =  cmd.BeginExecuteReader();
                //while (!result.IsCompleted)
                //{
                //    // while it isn't completed, wait.
                //}
                //if (result.IsCompleted)
                //{
                //    reader = cmd.EndExecuteReader(result);
                //}

                PropertyInfo[] props = ObjectHelper.GetReflectionProperties(new T());
                List<T> list = new List<T>();

                while (reader.Read())
                {
                    T obj = new T();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        try
                        {
                            PropertyInfo propertyInfo = obj.GetType().GetProperty(props[i].Name);
                            propertyInfo.SetValue(obj,
                                Convert.ChangeType(reader.GetValue(i), propertyInfo.PropertyType), null);
                        }
                        catch
                        {
                        }

                    }
                    
                    list.Add(obj);
                }

                reader.Close();
                dbCon.Close();
                return list;
            }

            return null;
        }
    }
}