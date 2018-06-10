using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite.Net.Async;

public static class DbException
{
    public static bool CheckDatabaseConnection(object conn)
    {
        if (conn == null)
        {
            DatabaseNotInitialized();
            return false;
        }

        return true;

    }
    public static void DatabaseNotInitialized()
    {
        throw new System.Exception("Database not initialized");
    }
}