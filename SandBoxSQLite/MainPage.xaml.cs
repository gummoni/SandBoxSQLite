using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace SandBoxSQLite
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            //migrate処理
            var filename = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "db.sqlite3");
            var db = new Connection(filename);
            var collection = db.CreateTable<TestModel>();
            var model = db.Create<TestModel>();
            model.Message = "Hello world";
            model.Update();
        }

        /// <summary>
        /// オブジェクトをJson文字列に変換する
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string ObjectToJson(object model)
        {
            if (null == model)
            {
                return "{}";
            }
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(model.GetType()).WriteObject(ms, model);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                return json;
            }
        }

        /// <summary>
        /// Json文字列をオブジェクトにする
        /// </summary>
        /// <param name="type"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static object JsonToModel(Type type, string context)
        {
            using (var ms = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(context);
                ms.Write(bytes, 0, bytes.Length);
                ms.Seek(0, SeekOrigin.Begin);
                var model = new DataContractJsonSerializer(type).ReadObject(ms);
                return model;
            }
        }


    }

    public class TestCollection : Table<TestModel>
    {
    }

    public class TestModel : Record
    {
        public string Message { get; set; }
    }

    public class TestController
    {
        public void Initialize()
        {
        }

        public void Dispose()
        {
            /*
            if (r == SQLite3.Result.Done) {
				int rowsAffected = SQLite3.Changes (Connection.Handle);
				SQLite3.Reset (Statement);
				return rowsAffected;
			} else if (r == SQLite3.Result.Error) {
				string msg = SQLite3.GetErrmsg (Connection.Handle);
				SQLite3.Reset (Statement);
				throw SQLiteException.New (r, msg);
			} else if (r == SQLite3.Result.Constraint && SQLite3.ExtendedErrCode (Connection.Handle) == SQLite3.ExtendedResult.ConstraintNotNull) {
				SQLite3.Reset (Statement);
				throw NotNullConstraintViolationException.New (r, SQLite3.GetErrmsg (Connection.Handle));
			} else {
				SQLite3.Reset (Statement);
				throw SQLiteException.New (r, r.ToString ());
			}

             */
        }
    }



    public class Connection
    {
        IntPtr handle;

        public Connection(string filename)
        {
            var result = SQLite3.Open(filename, out handle, (int)(SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite), IntPtr.Zero);
            if (result != SQLite3.Result.OK)
            {
                throw SQLiteException.New(result, $"cannot open database file: {filename} -> {result}");
            }
        }

        public void Dispose()
        {
            if (null != handle)
            {
                SQLite3.Close2(handle);
            }
        }

        Dictionary<Type, Func<IntPtr, int, object, int>> bindDic = new Dictionary<Type, Func<IntPtr, int, object, int>>();
        Dictionary<Type, Func<IntPtr, int, object>> colDic = new Dictionary<Type, Func<IntPtr, int, object>>();
        public T CreateTable<T>()
        {
            var result = Activator.CreateInstance<T>();
            ((INotifyCRUDChanged)result).Notification += ChangeEvent;


            //コマンド送信
            var command = typeof(T).SQLiteCreateTableCommand();
            var stmt = SQLite3.Prepare2(handle, command);
            var ret = SQLite3.Step(stmt);
            SQLite3.Finalize(stmt);



            //bindDic[typeof(byte)] = SQLite3.BindInt;

            //colDic[typeof(byte)] = SQLite3.ColumnInt;
            //colDic[typeof(sbyte)] = SQLite3.ColumnInt;
            //colDic[typeof(char)] = SQLite3.ColumnInt;
            //colDic[typeof(short)] = SQLite3.ColumnInt;
            //colDic[typeof(ushort)] = SQLite3.ColumnInt;
            //colDic[typeof(int)] = SQLite3.ColumnInt;
            //colDic[typeof(uint)] = SQLite3.ColumnInt;
            //colDic[typeof(long)] = SQLite3.ColumnInt;
            //colDic[typeof(ulong)] = SQLite3.ColumnInt;
            //colDic[typeof(byte[])] = SQLite3.ColumnInt;
            //colDic[typeof(decimal)] = SQLite3.ColumnDouble;
            //colDic[typeof(float)] = SQLite3.ColumnInt;
            //colDic[typeof(float)] = SQLite3.ColumnInt;

            return result;
        }

        public void DropTable<T>()
        {
            var command = typeof(T).SQLiteDropTableCommand();
            var stmt = SQLite3.Prepare2(handle, command);
            var ret = SQLite3.Step(stmt);
            SQLite3.Finalize(stmt);
        }

        public T Create<T>()
        {
            //挿入
            var model = Activator.CreateInstance(typeof(T));
            ((INotifyCRUDChanged)model).Notification += ChangeEvent;
            var tup = ((Record)model).SQLiteInsertCommand();
            var query = tup.Item1;
            var args = tup.Item2;
            var stmt = SQLite3.Prepare2(handle, query);
            BindAll(stmt, args);
            var ret = SQLite3.Step(stmt);
            SQLite3.Finalize(stmt);

            //ID取得
            query = ((Record)model).SQLiteGetLatestId();
            stmt = SQLite3.Prepare2(handle, query);
            ret = SQLite3.Step(stmt);
            ((Record)model).Id = SQLite3.ColumnInt(stmt, 0);
            SQLite3.Finalize(stmt);
            return (T)model;
        }


        //public void Update<T>(T model)
        //{
        //    ((Record)model).Update();
        //}

        public T ReadColumn<T>(IntPtr stmt, int index) => (T)colDic[typeof(T)](stmt, index);
        public int Bind<T>(IntPtr stmt, int index, object value) => bindDic[typeof(T)](stmt, index, value);


        public void ChangeEvent(object sender, CRUD args)
        {
            //テーブルのCRUD
            switch (args)
            {
                case CRUD.Create:
                    break;

                case CRUD.Read:
                    break;

                case CRUD.Update:
                    var tup = ((Record)sender).SqliteUpdateCommand();
                    var query = tup.Item1;
                    var _args = tup.Item2;
                    var stmt = SQLite3.Prepare2(handle, query);
                    BindAll(stmt, _args);
                    var ret = SQLite3.Step(stmt);
                    SQLite3.Finalize(stmt);
                    break;

                case CRUD.Delete:
                    break;
            }
        }

        internal static void BindAll(IntPtr stmt, object[] args)
        {
            var idx = 1;
            foreach (var arg in args)
            {
                BindParameter(stmt, idx++, arg);
            }
        }

        public static string SqlType(Type type, bool storeDateTimeAsTicks)
        {
            var clrType = type;
            if (clrType == typeof(Boolean) || clrType == typeof(Byte) || clrType == typeof(UInt16) || clrType == typeof(SByte) || clrType == typeof(Int16) || clrType == typeof(Int32))
            {
                return "integer";
            }
            else if (clrType == typeof(UInt32) || clrType == typeof(Int64))
            {
                return "bigint";
            }
            else if (clrType == typeof(Single) || clrType == typeof(Double) || clrType == typeof(Decimal))
            {
                return "float";
            }
            else if (clrType == typeof(String))
            {
                //int? len = p.MaxStringLength;

                //if (len.HasValue)
                //    return "varchar(" + len.Value + ")";

                return "varchar";
            }
            else if (clrType == typeof(TimeSpan))
            {
                return "bigint";
            }
            else if (clrType == typeof(DateTime))
            {
                return storeDateTimeAsTicks ? "bigint" : "datetime";
            }
            else if (clrType == typeof(DateTimeOffset))
            {
                return "bigint";
            }
            else if (clrType == typeof(Encoding))
            {
                return "integer";
            }
            else if (clrType.GetTypeInfo().IsEnum)
            {
                return "varchar";
            }
            else if (clrType == typeof(byte[]))
            {
                return "blob";
            }
            else if (clrType == typeof(Guid))
            {
                return "varchar(36)";
            }
            //else if (clrType.GetInterfaces().Contains(typeof(IModelBase)))
            //{
            //    return "integer";   //参照ID
            //}
            //else if (clrType.GetInterfaces().Contains(typeof(ICollectionBase)))
            //{
            //    return "varchar";   //参照ID
            //}
            else
            {
                //throw new NotSupportedException ("Don't know about " + clrType);
                return "varchar";   //other
            }
        }

        object ReadCol(IntPtr stmt, int index, SQLite3.ColType type, Type clrType)
        {
            if (type == SQLite3.ColType.Null)
            {
                return null;
            }
            else
            {
                if (clrType == typeof(String))
                {
                    return SQLite3.ColumnString(stmt, index);
                }
                else if (clrType == typeof(Int32))
                {
                    return (int)SQLite3.ColumnInt(stmt, index);
                }
                else if (clrType == typeof(Boolean))
                {
                    return SQLite3.ColumnInt(stmt, index) == 1;
                }
                else if (clrType == typeof(double))
                {
                    return SQLite3.ColumnDouble(stmt, index);
                }
                else if (clrType == typeof(float))
                {
                    return (float)SQLite3.ColumnDouble(stmt, index);
                }
                else if (clrType == typeof(TimeSpan))
                {
                    return new TimeSpan(SQLite3.ColumnInt64(stmt, index));
                }
                else if (clrType == typeof(DateTime))
                {
                    var text = SQLite3.ColumnString(stmt, index);
                    DateTime resultDate;
                    if (!DateTime.TryParseExact(text, DateTimeExactStoreFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out resultDate))
                    {
                        resultDate = DateTime.Parse(text);
                    }
                    return resultDate;
                }
                else if (clrType == typeof(DateTimeOffset))
                {
                    return new DateTimeOffset(SQLite3.ColumnInt64(stmt, index), TimeSpan.Zero);
                }
                else if (clrType == typeof(Encoding))
                {
                    return Encoding.GetEncoding(SQLite3.ColumnInt(stmt, index));
                }
                else if (clrType.GetTypeInfo().IsEnum)
                {
                    if (type == SQLite3.ColType.Text)
                    {
                        var value = SQLite3.ColumnString(stmt, index);
                        return Enum.Parse(clrType, value.ToString(), true);
                    }
                    else
                        return SQLite3.ColumnInt(stmt, index);
                }
                else if (clrType == typeof(Int64))
                {
                    return SQLite3.ColumnInt64(stmt, index);
                }
                else if (clrType == typeof(UInt64))
                {
                    return SQLite3.ColumnInt64(stmt, index);
                }
                else if (clrType == typeof(UInt32))
                {
                    return (uint)SQLite3.ColumnInt64(stmt, index);
                }
                else if (clrType == typeof(decimal))
                {
                    return (decimal)SQLite3.ColumnDouble(stmt, index);
                }
                else if (clrType == typeof(Byte))
                {
                    return (byte)SQLite3.ColumnInt(stmt, index);
                }
                else if (clrType == typeof(UInt16))
                {
                    return (ushort)SQLite3.ColumnInt(stmt, index);
                }
                else if (clrType == typeof(Int16))
                {
                    return (short)SQLite3.ColumnInt(stmt, index);
                }
                else if (clrType == typeof(sbyte))
                {
                    return (sbyte)SQLite3.ColumnInt(stmt, index);
                }
                //else if (clrType == typeof(byte[]))
                //{
                //    return SQLite3.ColumnByteArray(stmt, index);
                //}
                else if (clrType == typeof(Guid))
                {
                    var text = SQLite3.ColumnString(stmt, index);
                    return new Guid(text);
                }
                //else if (clrType.GetInterfaces().Contains(typeof(IModelBase)))
                //{
                //    return SQLite3.ColumnInt(stmt, index);      //DBから参照IDを取得する
                //}
                //else if (clrType.GetInterfaces().Contains(typeof(ICollectionBase)))
                //{
                //    return SQLite3.ColumnString(stmt, index);   //DBから参照IDリストを取得する
                //}
                else
                {
                    //throw new NotSupportedException ("Don't know how to read " + clrType);
                    return SQLite3.ColumnString(stmt, index);   //DBから構造体json情報を取得する
                }
            }
        }


        internal static void BindParameter(IntPtr stmt, int index, object value)
        {
            if (value == null)
            {
                SQLite3.BindNull(stmt, index);
            }
            else
            {
                var vtype = value.GetType();
                var vinfs = vtype.GetInterfaces();
                if (value is Int32)
                {
                    SQLite3.BindInt(stmt, index, (int)value);
                }
                else if (value is String)
                {
                    SQLite3.BindText(stmt, index, (string)value, -1, NegativePointer);
                }
                else if (value is Byte || value is UInt16 || value is SByte || value is Int16)
                {
                    SQLite3.BindInt(stmt, index, Convert.ToInt32(value));
                }
                else if (value is Boolean)
                {
                    SQLite3.BindInt(stmt, index, (bool)value ? 1 : 0);
                }
                else if (value is UInt32 || value is Int64)
                {
                    SQLite3.BindInt64(stmt, index, Convert.ToInt64(value));
                }
                else if (value is Single || value is Double || value is Decimal)
                {
                    SQLite3.BindDouble(stmt, index, Convert.ToDouble(value));
                }
                else if (value is TimeSpan)
                {
                    SQLite3.BindInt64(stmt, index, ((TimeSpan)value).Ticks);
                }
                else if (value is DateTime)
                {
                    SQLite3.BindText(stmt, index, ((DateTime)value).ToString(DateTimeExactStoreFormat, System.Globalization.CultureInfo.InvariantCulture), -1, NegativePointer);
                }
                else if (value is Encoding)
                {
                    SQLite3.BindText(stmt, index, ((Encoding)value).ToString(), -1, NegativePointer);
                }
                else if (value is DateTimeOffset)
                {
                    SQLite3.BindInt64(stmt, index, ((DateTimeOffset)value).UtcTicks);
                }
                else if (value.GetType().GetTypeInfo().IsEnum)
                {
                    if (value.GetType().GetTypeInfo().GetCustomAttribute(typeof(StoreAsTextAttribute), false) != null)
                        SQLite3.BindText(stmt, index, value.ToString(), -1, NegativePointer);
                    else
                        SQLite3.BindInt(stmt, index, Convert.ToInt32(value));
                }
                else if (value is byte[])
                {
                    SQLite3.BindBlob(stmt, index, (byte[])value, ((byte[])value).Length, NegativePointer);
                }
                else if (value is Guid)
                {
                    SQLite3.BindText(stmt, index, ((Guid)value).ToString(), 72, NegativePointer);
                }
                //else if (vinfs.Contains(typeof(IModelBase)))
                //{
                //    SQLite3.BindInt(stmt, index, (value as IModelBase).Id);    //モデルから参照IDを取ってくる
                //}
                //else if (vinfs.Contains(typeof(ICollectionBase)))
                //{
                //    SQLite3.BindText(stmt, index, (value as ICollectionBase).ToString(), -1, NegativePointer); //コレクションから参照IDリストを取ってくる
                //}
                //else
                //{
                //    //throw new NotSupportedException("Cannot store type: " + value.GetType());
                //    SQLite3.BindText(stmt, index, ObjectToJson(value), -1, NegativePointer);  //構造体はjson文字列
                //}
            }
        }

        const string DateTimeExactStoreFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff";
        internal static IntPtr NegativePointer = new IntPtr(-1);
    }


    [AttributeUsage(AttributeTargets.Enum)]
    public class StoreAsTextAttribute : Attribute
    {
    }

    public class Table<T> : INotifyCRUDChanged, IInitializable, IDisposable
    {
        ObservableCollection<T> items = new ObservableCollection<T>();
        public event EventHandler<CRUD> Notification;
        void OnNotification(object sender, [CallerMemberName] string name = "") => Notification?.Invoke(sender, (CRUD)Enum.Parse(typeof(CRUD), name));
        void OnNotification(object sender, CRUD crud) => Notification?.Invoke(sender, crud);

        public ObservableCollection<T> ToObservableCollection() => items;

        public T CreateRecord()
        {
            //レコード作成
            var model = Activator.CreateInstance<T>();
            ((INotifyCRUDChanged)model).Notification += ChangeEvent;
            OnNotification(model);
            return model;
        }

        /// <summary>
        /// レコード変更処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        void ChangeEvent(object sender, CRUD args)
        {
            //レコードのCRUD
            switch (args)
            {
                case CRUD.Update:
                case CRUD.Delete:
                    OnNotification(sender, args);
                    break;

                default:
                    throw new InvalidOperationException(args.ToString());
            }
        }

        public void Initialize()
        {
            foreach (var model in items)
            {
                ((IInitializable)model).Initialize();
                ((INotifyCRUDChanged)model).Notification += ChangeEvent;
            }
        }

        public void Dispose()
        {
            foreach (var model in items)
            {
                ((INotifyCRUDChanged)model).Notification -= ChangeEvent;
                ((IDisposable)model).Dispose();
            }
        }
    }

    public static class SQLiteExtensions
    {
        public static string SQLiteCreateTableCommand(this Type type)
        {
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = string.Join(", ", props.Select(_ => $"{_.Name} {_.SQLType()}"));
            return $"CREATE TABLE IF NOT EXISTS {type.Name}({fields});";
        }

        public static string SQLiteDropTableCommand(this Type type)
        {
            return $"DROP TABLE IF EXISTS {type.Name};";
        }

        public static Tuple<string, object[]> SQLiteInsertCommand(this Record record)
        {
            var type = record.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var field1 = string.Join(", ", props.Select(_ => $"{_.Name}"));
            var field2 = string.Join(", ", props.Select(_ => $"?"));
            var query = $"INSERT INTO {type.Name} ({field1}) VALUES ({field2});";
            var args = props.Select(_ => _.GetValue(record)).ToArray();
            return new Tuple<string, object[]>(query, args);
        }

        public static string SQLiteGetLatestId(this Record record)
        {
            var type = record.GetType();
            return $"SELECT MAX (Id) FROM {type.Name};";
        }

        public static Tuple<string, object[]> SqliteUpdateCommand(this Record record)
        {
            var type = record.GetType();
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(_ => null == _.GetCustomAttribute<PrimaryKeyAttribute>());
            var field = string.Join(", ", props.Select(_ => $"\"{_.Name}\" = ?"));
            var query = $"UPDATE \"{type.Name}\" SET {field} WHERE Id = ?;";
            var args = props.Select(_ => _.GetValue(record)).ToList();
            args.Add(type.GetProperty("Id").GetValue(record));
            return new Tuple<string, object[]>(query, args.ToArray());
        }
    }

    public class PrimaryKeyAttribute : Attribute
    {
    }

    public static class SQLiteExtentions
    {
        public static string SQLType(this PropertyInfo pinfo)
        {
            // Type -> 名前 型名 属性　の文字列に変換する（CreateTable用）
            var primarykey = pinfo.GetCustomAttribute<PrimaryKeyAttribute>();
            var type = pinfo.PropertyType;
            var result = "";

            if (type == typeof(int))
            {
                result = "INTEGER";
            }
            else if (type == typeof(string))
            {
                result = "varchar";
            }
            else
            {
                throw new InvalidCastException(type.Name);
            }

            if (null != primarykey)
            {
                result += " primary key autoincrement not null";
            }

            return result;
        }

        public static string SQLValue(this PropertyInfo pinfo, object sender)
        {
            var type = pinfo.PropertyType;
            var value = pinfo.GetValue(sender);
            if (type == typeof(int))
            {
                return value.ToString();
            }
            else if (type == typeof(string))
            {
                return value?.ToString();
            }
            else
            {
                throw new InvalidCastException(type.Name);
            }
        }
    }

    public class Record : INotifyCRUDChanged, IInitializable, IDisposable
    {
        [PrimaryKey]
        public int Id { get; set; }

        public event EventHandler<CRUD> Notification;
        void OnNotification([CallerMemberName] string name = "") => Notification?.Invoke(this, (CRUD)Enum.Parse(typeof(CRUD), name));

        public void Create()
        {
            OnNotification();
        }

        public void Read()
        {
            OnNotification();
        }

        public void Update()
        {
            OnNotification();
        }

        public void Delete()
        {
            OnNotification();
        }

        public void Initialize()
        {
        }

        public void Dispose()
        {
        }

    }

    public interface INotifyCRUDChanged
    {
        event EventHandler<CRUD> Notification;
    }

    public interface IInitializable
    {
        void Initialize();
    }

    public enum CRUD
    {
        Create,     //Insert
        Read,       //Select
        Update,     //Update
        Delete,     //Delete
    }

    public enum SQLiteType
    {
        NULL,       //NULL型
        INTEGER,    //符号付き整数 1, 2, 3, 4, 6, 8バイトで格納
        REAL,       //浮動小数点 8バイトで格納
        TEXT,       //テキスト(UTF-8)
        BLOB,       //入力データをそのまま格納
    }

    public static class SQLite3
    {
        public enum Result : int
        {
            OK = 0,
            Error = 1,
            Internal = 2,
            Perm = 3,
            Abort = 4,
            Busy = 5,
            Locked = 6,
            NoMem = 7,
            ReadOnly = 8,
            Interrupt = 9,
            IOError = 10,
            Corrupt = 11,
            NotFound = 12,
            Full = 13,
            CannotOpen = 14,
            LockErr = 15,
            Empty = 16,
            SchemaChngd = 17,
            TooBig = 18,
            Constraint = 19,
            Mismatch = 20,
            Misuse = 21,
            NotImplementedLFS = 22,
            AccessDenied = 23,
            Format = 24,
            Range = 25,
            NonDBFile = 26,
            Notice = 27,
            Warning = 28,
            Row = 100,
            Done = 101
        }

        public enum ExtendedResult : int
        {
            IOErrorRead = (Result.IOError | (1 << 8)),
            IOErrorShortRead = (Result.IOError | (2 << 8)),
            IOErrorWrite = (Result.IOError | (3 << 8)),
            IOErrorFsync = (Result.IOError | (4 << 8)),
            IOErrorDirFSync = (Result.IOError | (5 << 8)),
            IOErrorTruncate = (Result.IOError | (6 << 8)),
            IOErrorFStat = (Result.IOError | (7 << 8)),
            IOErrorUnlock = (Result.IOError | (8 << 8)),
            IOErrorRdlock = (Result.IOError | (9 << 8)),
            IOErrorDelete = (Result.IOError | (10 << 8)),
            IOErrorBlocked = (Result.IOError | (11 << 8)),
            IOErrorNoMem = (Result.IOError | (12 << 8)),
            IOErrorAccess = (Result.IOError | (13 << 8)),
            IOErrorCheckReservedLock = (Result.IOError | (14 << 8)),
            IOErrorLock = (Result.IOError | (15 << 8)),
            IOErrorClose = (Result.IOError | (16 << 8)),
            IOErrorDirClose = (Result.IOError | (17 << 8)),
            IOErrorSHMOpen = (Result.IOError | (18 << 8)),
            IOErrorSHMSize = (Result.IOError | (19 << 8)),
            IOErrorSHMLock = (Result.IOError | (20 << 8)),
            IOErrorSHMMap = (Result.IOError | (21 << 8)),
            IOErrorSeek = (Result.IOError | (22 << 8)),
            IOErrorDeleteNoEnt = (Result.IOError | (23 << 8)),
            IOErrorMMap = (Result.IOError | (24 << 8)),
            LockedSharedcache = (Result.Locked | (1 << 8)),
            BusyRecovery = (Result.Busy | (1 << 8)),
            CannottOpenNoTempDir = (Result.CannotOpen | (1 << 8)),
            CannotOpenIsDir = (Result.CannotOpen | (2 << 8)),
            CannotOpenFullPath = (Result.CannotOpen | (3 << 8)),
            CorruptVTab = (Result.Corrupt | (1 << 8)),
            ReadonlyRecovery = (Result.ReadOnly | (1 << 8)),
            ReadonlyCannotLock = (Result.ReadOnly | (2 << 8)),
            ReadonlyRollback = (Result.ReadOnly | (3 << 8)),
            AbortRollback = (Result.Abort | (2 << 8)),
            ConstraintCheck = (Result.Constraint | (1 << 8)),
            ConstraintCommitHook = (Result.Constraint | (2 << 8)),
            ConstraintForeignKey = (Result.Constraint | (3 << 8)),
            ConstraintFunction = (Result.Constraint | (4 << 8)),
            ConstraintNotNull = (Result.Constraint | (5 << 8)),
            ConstraintPrimaryKey = (Result.Constraint | (6 << 8)),
            ConstraintTrigger = (Result.Constraint | (7 << 8)),
            ConstraintUnique = (Result.Constraint | (8 << 8)),
            ConstraintVTab = (Result.Constraint | (9 << 8)),
            NoticeRecoverWAL = (Result.Notice | (1 << 8)),
            NoticeRecoverRollback = (Result.Notice | (2 << 8))
        }


        public enum ConfigOption : int
        {
            SingleThread = 1,
            MultiThread = 2,
            Serialized = 3
        }

        const string LibraryPath = "sqlite3";

#if !USE_CSHARP_SQLITE && !USE_WP8_NATIVE_SQLITE && !USE_SQLITEPCL_RAW
        [DllImport(LibraryPath, EntryPoint = "sqlite3_threadsafe", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Threadsafe();

        [DllImport(LibraryPath, EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Open([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr db);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Open([MarshalAs(UnmanagedType.LPStr)] string filename, out IntPtr db, int flags, IntPtr zvfs);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Open(byte[] filename, out IntPtr db, int flags, IntPtr zvfs);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_open16", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Open16([MarshalAs(UnmanagedType.LPWStr)] string filename, out IntPtr db);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_enable_load_extension", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result EnableLoadExtension(IntPtr db, int onoff);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Close(IntPtr db);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_close_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Close2(IntPtr db);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_initialize", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Initialize();

        [DllImport(LibraryPath, EntryPoint = "sqlite3_shutdown", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Shutdown();

        [DllImport(LibraryPath, EntryPoint = "sqlite3_config", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Config(ConfigOption option);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_win32_set_directory", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int SetDirectory(uint directoryType, string directoryPath);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_busy_timeout", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result BusyTimeout(IntPtr db, int milliseconds);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_changes", CallingConvention = CallingConvention.Cdecl)]
        public static extern int Changes(IntPtr db);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Prepare2(IntPtr db, [MarshalAs(UnmanagedType.LPStr)] string sql, int numBytes, out IntPtr stmt, IntPtr pzTail);

#if NETFX_CORE
        [DllImport(LibraryPath, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Prepare2(IntPtr db, byte[] queryBytes, int numBytes, out IntPtr stmt, IntPtr pzTail);
#endif

        public static IntPtr Prepare2(IntPtr db, string query)
        {
            IntPtr stmt;
#if NETFX_CORE
            byte[] queryBytes = System.Text.UTF8Encoding.UTF8.GetBytes(query);
            var r = Prepare2(db, queryBytes, queryBytes.Length, out stmt, IntPtr.Zero);
#else
            var r = Prepare2 (db, query, System.Text.UTF8Encoding.UTF8.GetByteCount (query), out stmt, IntPtr.Zero);
#endif
            if (r != Result.OK)
            {
                throw SQLiteException.New(r, GetErrmsg(db));
            }
            return stmt;
        }

        [DllImport(LibraryPath, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Step(IntPtr stmt);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_reset", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Reset(IntPtr stmt);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        public static extern Result Finalize(IntPtr stmt);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_last_insert_rowid", CallingConvention = CallingConvention.Cdecl)]
        public static extern long LastInsertRowid(IntPtr db);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_errmsg16", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr Errmsg(IntPtr db);

        public static string GetErrmsg(IntPtr db)
        {
            return Marshal.PtrToStringUni(Errmsg(db));
        }

        [DllImport(LibraryPath, EntryPoint = "sqlite3_bind_parameter_index", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindParameterIndex(IntPtr stmt, [MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_bind_null", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindNull(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_bind_int", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindInt(IntPtr stmt, int index, int val);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_bind_int64", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindInt64(IntPtr stmt, int index, long val);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_bind_double", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindDouble(IntPtr stmt, int index, double val);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_bind_text16", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern int BindText(IntPtr stmt, int index, [MarshalAs(UnmanagedType.LPWStr)] string val, int n, IntPtr free);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Cdecl)]
        public static extern int BindBlob(IntPtr stmt, int index, byte[] val, int n, IntPtr free);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_count", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ColumnCount(IntPtr stmt);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_name", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ColumnName(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_name16", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr ColumnName16Internal(IntPtr stmt, int index);
        public static string ColumnName16(IntPtr stmt, int index)
        {
            return Marshal.PtrToStringUni(ColumnName16Internal(stmt, index));
        }

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_type", CallingConvention = CallingConvention.Cdecl)]
        public static extern ColType ColumnType(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_int", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ColumnInt(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_int64", CallingConvention = CallingConvention.Cdecl)]
        public static extern long ColumnInt64(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_double", CallingConvention = CallingConvention.Cdecl)]
        public static extern double ColumnDouble(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ColumnText(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_text16", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ColumnText16(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ColumnBlob(IntPtr stmt, int index);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        public static extern int ColumnBytes(IntPtr stmt, int index);

        public static string ColumnString(IntPtr stmt, int index)
        {
            return Marshal.PtrToStringUni((IntPtr)ColumnText16(stmt, index));
        }

        //public static byte[] ColumnByteArray(IntPtr stmt, int index)
        //{
        //    int length = ColumnBytes(stmt, index);
        //    var result = new byte[length];
        //    if (length > 0)
        //        Marshal.Copy(ColumnBlob(stmt, index), result, 0, length);
        //    return result;
        //}

        [DllImport(LibraryPath, EntryPoint = "sqlite3_extended_errcode", CallingConvention = CallingConvention.Cdecl)]
        public static extern ExtendedResult ExtendedErrCode(IntPtr db);

        [DllImport(LibraryPath, EntryPoint = "sqlite3_libversion_number", CallingConvention = CallingConvention.Cdecl)]
        public static extern int LibVersionNumber();
#else
		public static Result Open(string filename, out Sqlite3DatabaseHandle db)
		{
			return (Result) Sqlite3.sqlite3_open(filename, out db);
		}

		public static Result Open(string filename, out Sqlite3DatabaseHandle db, int flags, IntPtr zVfs)
		{
#if USE_WP8_NATIVE_SQLITE
			return (Result)Sqlite3.sqlite3_open_v2(filename, out db, flags, "");
#else
			return (Result)Sqlite3.sqlite3_open_v2(filename, out db, flags, null);
#endif
		}

		public static Result Close(Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_close(db);
		}

		public static Result Close2(Sqlite3DatabaseHandle db)
		{
			return (Result)Sqlite3.sqlite3_close_v2(db);
		}

		public static Result BusyTimeout(Sqlite3DatabaseHandle db, int milliseconds)
		{
			return (Result)Sqlite3.sqlite3_busy_timeout(db, milliseconds);
		}

		public static int Changes(Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_changes(db);
		}

		public static Sqlite3Statement Prepare2(Sqlite3DatabaseHandle db, string query)
		{
			Sqlite3Statement stmt = default(Sqlite3Statement);
#if USE_WP8_NATIVE_SQLITE || USE_SQLITEPCL_RAW
			var r = Sqlite3.sqlite3_prepare_v2(db, query, out stmt);
#else
			stmt = new Sqlite3Statement();
			var r = Sqlite3.sqlite3_prepare_v2(db, query, -1, ref stmt, 0);
#endif
			if (r != 0)
			{
				throw SQLiteException.New((Result)r, GetErrmsg(db));
			}
			return stmt;
		}

		public static Result Step(Sqlite3Statement stmt)
		{
			return (Result)Sqlite3.sqlite3_step(stmt);
		}

		public static Result Reset(Sqlite3Statement stmt)
		{
			return (Result)Sqlite3.sqlite3_reset(stmt);
		}

		public static Result Finalize(Sqlite3Statement stmt)
		{
			return (Result)Sqlite3.sqlite3_finalize(stmt);
		}

		public static long LastInsertRowid(Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_last_insert_rowid(db);
		}

		public static string GetErrmsg(Sqlite3DatabaseHandle db)
		{
			return Sqlite3.sqlite3_errmsg(db);
		}

		public static int BindParameterIndex(Sqlite3Statement stmt, string name)
		{
			return Sqlite3.sqlite3_bind_parameter_index(stmt, name);
		}

		public static int BindNull(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_bind_null(stmt, index);
		}

		public static int BindInt(Sqlite3Statement stmt, int index, int val)
		{
			return Sqlite3.sqlite3_bind_int(stmt, index, val);
		}

		public static int BindInt64(Sqlite3Statement stmt, int index, long val)
		{
			return Sqlite3.sqlite3_bind_int64(stmt, index, val);
		}

		public static int BindDouble(Sqlite3Statement stmt, int index, double val)
		{
			return Sqlite3.sqlite3_bind_double(stmt, index, val);
		}

		public static int BindText(Sqlite3Statement stmt, int index, string val, int n, IntPtr free)
		{
#if USE_WP8_NATIVE_SQLITE
			return Sqlite3.sqlite3_bind_text(stmt, index, val, n);
#elif USE_SQLITEPCL_RAW
			return Sqlite3.sqlite3_bind_text(stmt, index, val);
#else
			return Sqlite3.sqlite3_bind_text(stmt, index, val, n, null);
#endif
		}

		public static int BindBlob(Sqlite3Statement stmt, int index, byte[] val, int n, IntPtr free)
		{
#if USE_WP8_NATIVE_SQLITE
			return Sqlite3.sqlite3_bind_blob(stmt, index, val, n);
#elif USE_SQLITEPCL_RAW
			return Sqlite3.sqlite3_bind_blob(stmt, index, val);
#else
			return Sqlite3.sqlite3_bind_blob(stmt, index, val, n, null);
#endif
		}

		public static int ColumnCount(Sqlite3Statement stmt)
		{
			return Sqlite3.sqlite3_column_count(stmt);
		}

		public static string ColumnName(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_name(stmt, index);
		}

		public static string ColumnName16(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_name(stmt, index);
		}

		public static ColType ColumnType(Sqlite3Statement stmt, int index)
		{
			return (ColType)Sqlite3.sqlite3_column_type(stmt, index);
		}

		public static int ColumnInt(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_int(stmt, index);
		}

		public static long ColumnInt64(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_int64(stmt, index);
		}

		public static double ColumnDouble(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_double(stmt, index);
		}

		public static string ColumnText(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_text(stmt, index);
		}

		public static string ColumnText16(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_text(stmt, index);
		}

		public static byte[] ColumnBlob(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_blob(stmt, index);
		}

		public static int ColumnBytes(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_bytes(stmt, index);
		}

		public static string ColumnString(Sqlite3Statement stmt, int index)
		{
			return Sqlite3.sqlite3_column_text(stmt, index);
		}

		public static byte[] ColumnByteArray(Sqlite3Statement stmt, int index)
		{
			return ColumnBlob(stmt, index);
		}

#if !USE_SQLITEPCL_RAW
		public static Result EnableLoadExtension(Sqlite3DatabaseHandle db, int onoff)
		{
			return (Result)Sqlite3.sqlite3_enable_load_extension(db, onoff);
		}
#endif

		public static ExtendedResult ExtendedErrCode(Sqlite3DatabaseHandle db)
		{
			return (ExtendedResult)Sqlite3.sqlite3_extended_errcode(db);
		}
#endif

        public enum ColType : int
        {
            Integer = 1,
            Float = 2,
            Text = 3,
            Blob = 4,
            Null = 5
        }
    }

    public class SQLiteException : Exception
    {
        public SQLite3.Result Result { get; private set; }

        protected SQLiteException(SQLite3.Result r, string message) : base(message)
        {
            Result = r;
        }

        public static SQLiteException New(SQLite3.Result r, string message)
        {
            return new SQLiteException(r, message);
        }
    }

    //public class NotNullConstraintViolationException : SQLiteException
    //{
    //    public IEnumerable<TableMapping.Column> Columns { get; protected set; }

    //    protected NotNullConstraintViolationException(SQLite3.Result r, string message)
    //        : this(r, message, null, null)
    //    {

    //    }

    //    protected NotNullConstraintViolationException(SQLite3.Result r, string message, TableMapping mapping, object obj)
    //        : base(r, message)
    //    {
    //        if (mapping != null && obj != null)
    //        {
    //            this.Columns = from c in mapping.Columns
    //                           where c.IsNullable == false && c.GetValue(obj) == null
    //                           select c;
    //        }
    //    }

    //    public static new NotNullConstraintViolationException New(SQLite3.Result r, string message)
    //    {
    //        return new NotNullConstraintViolationException(r, message);
    //    }

    //    public static NotNullConstraintViolationException New(SQLite3.Result r, string message, TableMapping mapping, object obj)
    //    {
    //        return new NotNullConstraintViolationException(r, message, mapping, obj);
    //    }

    //    public static NotNullConstraintViolationException New(SQLiteException exception, TableMapping mapping, object obj)
    //    {
    //        return new NotNullConstraintViolationException(exception.Result, exception.Message, mapping, obj);
    //    }
    //}

    [Flags]
    public enum SQLiteOpenFlags
    {
        ReadOnly = 1, ReadWrite = 2, Create = 4,
        NoMutex = 0x8000, FullMutex = 0x10000,
        SharedCache = 0x20000, PrivateCache = 0x40000,
        ProtectionComplete = 0x00100000,
        ProtectionCompleteUnlessOpen = 0x00200000,
        ProtectionCompleteUntilFirstUserAuthentication = 0x00300000,
        ProtectionNone = 0x00400000
    }

    [Flags]
    public enum CreateFlags
    {
        None = 0x000,
        ImplicitPK = 0x001,    // create a primary key for field called 'Id' (Orm.ImplicitPkName)
        ImplicitIndex = 0x002,    // create an index for fields ending in 'Id' (Orm.ImplicitIndexSuffix)
        AllImplicit = 0x003,    // do both above
        AutoIncPK = 0x004,    // force PK field to be auto inc
        FullTextSearch3 = 0x100,    // create virtual table using FTS3
        FullTextSearch4 = 0x200     // create virtual table using FTS4
    }


}
