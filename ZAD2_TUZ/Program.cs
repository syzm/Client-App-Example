using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Transactions;

// trzeba dodać System.Data.SqlClient używając Project->Manage nuget packages

namespace ZAD2_TUZ
{
    public static class Program
    {
        // swoje tabele utworzylem w bazie pomocniczej bazka
        const string connectionString = "Data Source=tcp:localhost;Initial Catalog=bazka;User ID=sa;Password=bazyhaslo";

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Connected!");
                TransactionA();
                DisplayTables();
                TransactionB();
                DisplayTables();
                TransactionC();
                DisplayTables();
                TransactionD();
                DisplayTables();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        static void TransactionD()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // korzystam z https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16
                        // i.
                        using (SqlCommand command = new SqlCommand("UPDATE Jobs SET Execution_date = DATEADD(WEEK, 1, Execution_date)", connection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        // ii.
                        StringBuilder sb = new StringBuilder();
                        sb.Append("INSERT INTO Jobs (Rate_Per_Hour, Execution_date, Worker_id) ");
                        sb.Append(
                            "SELECT AVG(Rate_Per_Hour), DATEADD(DAY, -1, GETDATE()), Worker_id From Jobs GROUP BY Worker_id");
                        using (SqlCommand command = new SqlCommand(sb.ToString(), connection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        Console.WriteLine("Transaction D commited successfully");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine("Error in Transaction D: " + ex.Message);
                    }
                }
            }
        }


        static void TransactionC()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // Start and end of the previous month
                        DateTime currentDate = DateTime.Now;
                        DateTime startDate = new DateTime(currentDate.Year, currentDate.Month, 1).AddMonths(-1);
                        DateTime endDate = new DateTime(currentDate.Year, currentDate.Month, 1).AddDays(-1);

                        StringBuilder sb = new StringBuilder();
                        sb.Append("SELECT j.Job_id, j.Execution_date, j.Rate_per_hour, j.Worker_id, e.Nr_tel FROM Jobs j INNER JOIN Employees E ON j.Worker_id = e.Worker_id ");
                        sb.Append("WHERE (j.Execution_date >= @StartDate AND j.Execution_date <= @EndDate)");

                        using (SqlCommand command = new SqlCommand(sb.ToString(), connection, transaction))
                        {
                            command.Parameters.AddWithValue("@StartDate", startDate);
                            command.Parameters.AddWithValue("@EndDate", endDate);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                Console.WriteLine("Zadania z poprzedniego miesiąca:");
                                Console.WriteLine("Id zadania\tStawka godz\tData wykonania\t\tId pracownika\tNr tel");
                                Console.WriteLine("----------------------------------------------------------------");

                                while (reader.Read())
                                {
                                    int jobId = reader.GetInt32(0);
                                    DateTime executionDate = reader.GetDateTime(1);
                                    decimal rate = reader.GetDecimal(2);
                                    int workerId = reader.GetInt32(3);
                                    string phoneNumber = reader.GetString(4);

                                    Console.WriteLine($"{jobId}\t\t{rate}\t\t{executionDate}\t{workerId}\t\t{phoneNumber}");
                                }
                            }
                        }

                        transaction.Commit();
                        Console.WriteLine("Transaction C committed successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine("Error in Transaction C: " + ex.Message);
                    }
                }
            }
        }

        static void TransactionB()
        {
            Console.Write("Wpisz numer telefonu (do transakcji B): ");
            string phoneNumber = Console.ReadLine();

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int workerId;
                        using (SqlCommand command =
                               new SqlCommand("SELECT Worker_id FROM Employees WHERE Nr_tel = @Nr_tel", connection,
                                   transaction))
                        {
                            command.Parameters.AddWithValue("@Nr_tel", phoneNumber);
                            object result = command.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                workerId = (int) result;
                            }
                            else
                            {
                                Console.WriteLine("Brak klienta z danym numerem telefonu.");
                                return;
                            }
                        }

                        StringBuilder sb = new StringBuilder();

                        sb.Append("DELETE FROM Jobs WHERE Worker_id = @Worker_id AND ");
                        sb.Append(
                            "Execution_date = (SELECT MIN(Execution_date) FROM Jobs WHERE Worker_id = @Worker_id)");

                        using (SqlCommand command = new SqlCommand(sb.ToString(), connection, transaction))
                        {
                            command.Parameters.AddWithValue("@Worker_id", workerId);
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        Console.WriteLine("Transaction B committed successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine("Error in Transaction B " + ex.Message);
                    }
                }
            }
        }

        static void ClearTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        using (SqlCommand command = new SqlCommand("DELETE FROM Employees", connection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        using (SqlCommand command = new SqlCommand("DELETE FROM Jobs", connection, transaction))
                        {
                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
            }
        }

        static void TransactionA()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        Console.Write("Wpisz ile rekordow do 'Employees' chcesz dodac: ");
                        int customerAmount = int.Parse(Console.ReadLine());

                        for (int i = 0; i < customerAmount; ++i)
                        {
                            Console.Write("Wpisz numer telefonu (9 cyfr, bez spacji): ");
                            string phoneNumber = Console.ReadLine();

                            using (SqlCommand command =
                                   new SqlCommand("INSERT INTO Employees (Nr_tel) VALUES (@Nr_tel)", connection,
                                       transaction))
                            {
                                command.Parameters.AddWithValue("@Nr_tel", phoneNumber);
                                command.ExecuteNonQuery();
                            }
                            Console.WriteLine();
                        }

                        Console.WriteLine("\nExisting Worker IDs:");
                        using (SqlCommand command =
                               new SqlCommand("SELECT Worker_id FROM Employees", connection, transaction))
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int clientId = reader.GetInt32(0);
                                    Console.WriteLine(clientId);
                                }
                            }
                        }

                        Console.Write("Wpisz ile rekordow do 'Jobs' chcesz dodac: ");
                        int jobAmount = int.Parse(Console.ReadLine());

                        for (int i = 0; i < jobAmount; i++)
                        {
                            Console.Write("Wpisz stawke godzinowa (XYZ.BC): ");
                            decimal hourRate = decimal.Parse(Console.ReadLine(), CultureInfo.InvariantCulture);

                            Console.Write("Wpisz date wykonania (yyyy-MM-dd HH:mm:ss): ");
                            DateTime execDate = DateTime.Parse(Console.ReadLine());

                            Console.Write("Wpisz id pracownika: ");
                            int workerId = int.Parse(Console.ReadLine());

                            using (SqlCommand command = new SqlCommand(
                                       "INSERT INTO Jobs (Rate_Per_Hour, Execution_date, Worker_id) VALUES (@Rate_Per_Hour, @Execution_date, @Worker_id)",
                                       connection, transaction))
                            {
                                command.Parameters.AddWithValue("@Rate_Per_Hour", hourRate);
                                command.Parameters.AddWithValue("@Execution_date", execDate);
                                command.Parameters.AddWithValue("@Worker_id", workerId);
                                command.ExecuteNonQuery();
                            }
                            Console.WriteLine();
                        }

                        transaction.Commit();
                        Console.WriteLine("Transaction A committed successfully.");
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine("Error in Transaction A: " + ex.Message);
                    }
                }
            }
        }

        static void DisplayTables()
        {
            Console.WriteLine();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                try
                {
                    SqlCommand command1 = new SqlCommand("SELECT * FROM Employees", connection);
                    SqlDataReader reader1 = command1.ExecuteReader();
                    Console.WriteLine("Pracownicy: ");
                    Console.WriteLine("---------------------------------------------------------------------");
                    Console.WriteLine("Id pracownika\tNr telefonu");
                    Console.WriteLine("---------------------------------------------------------------------");
                    while (reader1.Read())
                    {
                        Console.WriteLine($"{reader1["Worker_id"]}\t\t{reader1["Nr_tel"]}");
                    }
                    reader1.Close();
                    Console.WriteLine();

                    SqlCommand command2 = new SqlCommand("SELECT * FROM Jobs", connection);
                    SqlDataReader reader2 = command2.ExecuteReader();
                    Console.WriteLine("Zadania: ");
                    Console.WriteLine("---------------------------------------------------------------------");
                    Console.WriteLine("Id zadania\tStawka godz\tData wykonania\t\tId pracownika");
                    Console.WriteLine("---------------------------------------------------------------------");
                    while (reader2.Read())
                    {
                        Console.WriteLine($"{reader2["Job_id"]}\t\t{reader2["Rate_per_hour"]}\t\t{reader2["Execution_date"]}\t{reader2["Worker_id"]}");
                    }
                    reader2.Close();
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error while Displaying Tables: " + ex.Message);
                }
            }
        }


    }
}