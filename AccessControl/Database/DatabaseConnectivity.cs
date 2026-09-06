using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace AccessControl.Database
{
    public class DatabaseConnectivity
    {

        private SqlConnection _sqlConnection;
        private bool _keepAlive;
        private readonly Dictionary<string, HashSet<string>> _procedureParameters = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _procedureParametersLock = new();
        public DatabaseConnectivity(string connectionString, bool keepAlive = false)
        {
            _sqlConnection = new SqlConnection(connectionString);
            if (keepAlive)
            {
                _sqlConnection.Open();
            }
            _keepAlive = keepAlive;
        }

        public void CloseConnection()
        {
            _sqlConnection?.Close();
        }

        private SqlParameter[] BindableParameters(string spName, SqlParameter[]? sqlParams)
        {
            if (sqlParams == null || sqlParams.Length == 0)
            {
                return Array.Empty<SqlParameter>();
            }
            HashSet<string>? allowed = LoadProcedureParameters(spName);
            if (allowed == null || allowed.Count == 0)
            {
                return sqlParams;
            }
            return sqlParams.Where(p => allowed.Contains(p.ParameterName)).ToArray();
        }

        private HashSet<string>? LoadProcedureParameters(string spName)
        {
            lock (_procedureParametersLock)
            {
                if (_procedureParameters.TryGetValue(spName, out HashSet<string>? cached))
                {
                    return cached;
                }
            }

            bool openedHere = false;
            try
            {
                if (_sqlConnection.State != ConnectionState.Open)
                {
                    _sqlConnection.Open();
                    openedHere = true;
                }
                using SqlCommand cmd = new(
                    "SELECT [name] FROM sys.parameters WHERE [object_id] = OBJECT_ID(@n)",
                    _sqlConnection);
                cmd.Parameters.AddWithValue("@n", spName.Contains('.') ? spName : "dbo." + spName);
                HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            names.Add(reader.GetString(0));
                        }
                    }
                }
                lock (_procedureParametersLock)
                {
                    _procedureParameters[spName] = names;
                }
                return names;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (openedHere && !_keepAlive && _sqlConnection.State == ConnectionState.Open)
                {
                    _sqlConnection.Close();
                }
            }
        }


        public SqlParameter GenerateInputInteger(string paramName, int val)
        {
            return new SqlParameter(paramName, SqlDbType.Int, 0)
            {
                Value = val
            };
        }

        public SqlParameter GenerateInputDecimal(string paramName, decimal val)
        {
            return new SqlParameter(paramName, SqlDbType.Decimal, 0)
            {
                Value = val
            };
        }


        public SqlParameter GenerateInputString(string paramName, string? val, int length = 2000)
        {
            return new SqlParameter(paramName, SqlDbType.NVarChar, length)
            {
                Value = (object?)val ?? DBNull.Value
            };
        }
        public SqlParameter GenerateInputVarBinary(string paramName, byte val, int length = 2000)
        {
            return new SqlParameter(paramName, SqlDbType.VarBinary, length)
            {
                Value = (object)val ?? DBNull.Value
            };
        }
        public SqlParameter GenerateInputUniqueIdentifier(string paramName, Guid val, int length = 2000)
        {
            return new SqlParameter(paramName, SqlDbType.UniqueIdentifier, length)
            {
                Value = (object)val ?? DBNull.Value
            };
        }

        public SqlParameter GenerateInputDateTime(string paramName, DateTime val)
        {
            return new SqlParameter(paramName, SqlDbType.DateTime)
            {
                Value = val
            };
        }
        public SqlParameter GenerateInputTime(string paramName, TimeSpan val)
        {
            return new SqlParameter(paramName, SqlDbType.Time)
            {
                Value = val
            };
        }

        public SqlParameter GenerateInputint(string paramName, int val)
        {
            return new SqlParameter(paramName, SqlDbType.BigInt, 0)
            {
                Value = val
            };
        }

        public SqlParameter GenerateInputBool(string paramName, bool val)
        {
            return new SqlParameter(paramName, SqlDbType.Bit, 0)
            {
                Value = val
            };
        }

        public SqlParameter GenerateInputCurrency(string paramName, decimal val)
        {
            return new SqlParameter(paramName, SqlDbType.Money, 0)
            {
                Value = val,
                Precision = 4
            };
        }

        public SqlParameter GenerateInputStringList(string paramName, DataTable vals)
        {

            SqlParameter ret = new()
            {
                Value = vals,
                TypeName = "dbo.StringList",
                SqlDbType = SqlDbType.Structured,
                ParameterName = paramName
            };

            return ret;

        }

        public SqlParameter GenerateInputIntList(string paramName, DataTable vals)
        {

            SqlParameter ret = new()
            {
                Value = vals,
                TypeName = "dbo.IntList",
                SqlDbType = SqlDbType.Structured,
                ParameterName = paramName
            };

            return ret;

        }

        public DataTable ExecuteStoredProcedureAsDataTable(string spName, SqlParameter[]? sqlParams)
        {
            Exception thrownException = null!;
            DataTable table = new DataTable();
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                SqlCommand sqlCommand = new SqlCommand(spName, _sqlConnection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                if (sqlParams != null)
                {
                    sqlCommand.Parameters.AddRange(BindableParameters(spName, sqlParams));
                }
                SqlDataAdapter adapter = new SqlDataAdapter(sqlCommand);
                adapter.Fill(table);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }
            else
            {
                return table;
            }
        }


        public DataRow ExecuteStoredProcedureAsDataRow(string spName, SqlParameter[]? sqlParams)
        {
            Exception thrownException = null!;
            DataTable table = new DataTable();
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                SqlCommand sqlCommand = new SqlCommand(spName, _sqlConnection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                if (sqlParams != null)
                {
                    sqlCommand.Parameters.AddRange(BindableParameters(spName, sqlParams));
                }
                SqlDataAdapter adapter = new SqlDataAdapter(sqlCommand);
                adapter.Fill(table);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }
            else
            {
                return table.Rows.Count > 0 ? table.Rows[0] : null!;
            }
        }


        public void ExecuteStoredProcedureNoReturn(string spName, SqlParameter[]? sqlParams)
        {
            Exception thrownException = null!;
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                SqlCommand sqlCommand = new SqlCommand(spName, _sqlConnection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                if (sqlParams != null)
                {
                    sqlCommand.Parameters.AddRange(BindableParameters(spName, sqlParams));
                }
                sqlCommand.ExecuteNonQuery();

            }
            catch (Exception ex)
            {
                thrownException = ex;

            }
            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }

        }

        public int ExecuteStoredProcedureAsScalarInt(string spName, SqlParameter[]? sqlParams)
        {
            Exception thrownException = null!;
            int scalarInt = 0;
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                SqlCommand sqlCommand = new SqlCommand(spName, _sqlConnection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                if (sqlParams != null)
                {
                    sqlCommand.Parameters.AddRange(BindableParameters(spName, sqlParams));
                }
                scalarInt = Convert.ToInt32(sqlCommand.ExecuteScalar());
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }
            else
            {
                return scalarInt;
            }


        }

        //public int ExecuteStoredProcedureAsScalarint(string spName, SqlParameter[]? sqlParams)
        //{
        //    Exception thrownException = null!;
        //    int scalarint = 0;
        //    try
        //    {
        //        if (!_keepAlive)
        //        {
        //            _sqlConnection.Open();
        //        }
        //        SqlCommand sqlCommand = new SqlCommand(spName, _sqlConnection);
        //        sqlCommand.CommandType = CommandType.StoredProcedure;
        //        if (sqlParams != null)
        //        {
        //            sqlCommand.Parameters.AddRange(sqlParams);
        //        }
        //        scalarint = Convert.ToInt64(sqlCommand.ExecuteScalar());
        //    }
        //    catch (Exception ex)
        //    {
        //        thrownException = ex;
        //    }
        //    finally
        //    {
        //        if (!_keepAlive)
        //        {
        //            _sqlConnection.Close();
        //        }
        //    }
        //    if (thrownException != null)
        //    {
        //        throw thrownException;
        //    }
        //    else
        //    {
        //        return scalarint;
        //    }

        //}

        public string? ExecuteStoredProcedureAsScalarString(string spName, SqlParameter[]? sqlParams)
        {
            Exception thrownException = null!;
            string scalarString = "";
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                SqlCommand sqlCommand = new SqlCommand(spName, _sqlConnection);
                sqlCommand.CommandType = CommandType.StoredProcedure;
                if (sqlParams != null)
                {
                    sqlCommand.Parameters.AddRange(BindableParameters(spName, sqlParams));
                }
                scalarString = Convert.ToString(sqlCommand.ExecuteScalar())!;
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }
            else
            {
                return scalarString;
            }
        }




        public DataTable ExecuteRawSQLAsDataTable(string sql)
        {
            Exception thrownException = null!;
            DataTable table = new DataTable();
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                SqlCommand sqlCommand = new SqlCommand(sql, _sqlConnection);
                SqlDataAdapter adapter = new SqlDataAdapter(sqlCommand);
                adapter.Fill(table);
            }
            catch (Exception ex)
            {
                thrownException = ex;
            }
            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }
            else
            {
                return table;
            }
        }


        public DataRow ExecuteRawSQLAsDataRow(string sql)
        {
            Exception thrownException = null!;
            DataRow row = null!;
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                DataTable table = new DataTable();
                SqlCommand sqlCommand = new SqlCommand(sql, _sqlConnection);
                SqlDataAdapter adapter = new SqlDataAdapter(sqlCommand);

                adapter.Fill(table);
                row = table.Rows[0];
            }

            catch (Exception ex)
            {
                thrownException = ex;
            }

            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }
            else
            {
                return row;
            }
        }


        public void ExecuteRawSQLNoReturn(string sql)
        {
            Exception thrownException = null!;
            try
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Open();
                }
                SqlCommand sqlCommand = new SqlCommand(sql, _sqlConnection);

                sqlCommand.ExecuteNonQuery();
            }

            catch (Exception ex)
            {
                thrownException = ex;
            }

            finally
            {
                if (!_keepAlive)
                {
                    _sqlConnection.Close();
                }
            }
            if (thrownException != null)
            {
                throw thrownException;
            }
        }

    }
}
