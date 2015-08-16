
namespace Perst
{
    using System;
    
    /// <summary> Exception throw by storage implementation
    /// </summary>
    public class StorageError:Exception
    {
        /// <summary> Get exception error code (see definitions above)
        /// </summary>
        public virtual ErrorCode Code
        {
            get
            {
                return errorCode;
            }
            
        }

        /// <summary> Get original exception if StorageError excepotion was thrown as the result 
        /// of catching some other exception within Storage implementation. 
        /// StorageError is used as wrapper of other exceptions to avoid cascade propagation
        /// of throws and try/catch constructions.
        /// </summary>
        /// <returns>original exception or <b>null</b> if there is no such exception
        /// 
        /// </returns>
        public virtual Exception OriginalException
        {
            get
            {
                return origEx;
            }
            
        }
        public enum ErrorCode 
        {
            STORAGE_NOT_OPENED,
            STORAGE_ALREADY_OPENED,
            FILE_ACCESS_ERROR,
            KEY_NOT_UNIQUE,
            KEY_NOT_FOUND,
            SCHEMA_CHANGED,
            UNSUPPORTED_TYPE,
            UNSUPPORTED_INDEX_TYPE,
            INCOMPATIBLE_KEY_TYPE,
            INCOMPATIBLE_VALUE_TYPE,
            NOT_ENOUGH_SPACE,
            DATABASE_CORRUPTED,
            CONSTRUCTOR_FAILURE,
            DESCRIPTOR_FAILURE,
            ACCESS_TO_STUB,
            INVALID_OID,
            DELETED_OBJECT,
            ACCESS_VIOLATION,
            CLASS_NOT_FOUND,
            AMBIGUITY_CLASS,
            INDEXED_FIELD_NOT_FOUND,
            NO_SUCH_PROPERTY,
            BAD_PROPERTY_VALUE,
            BAD_REPLICATION_PORT,
            CONNECTION_FAILURE,
            TOO_MUCH_OBJECTS,
            REPLICA_MODIFICATION,
            KEY_IS_NULL,
            INVALID_OPERATION,
            READ_ONLY_DATABASE,
            NOT_IN_TRANSACTION,
            WRONG_CIPHER_KEY
        };

        private static string[] messageText = new string[]        
        {
            "Storage not opened", 
            "Storage already opened", 
            "File access error", 
            "Key not unique", 
            "Key not found", 
            "Database schema was changed for", 
            "Unsupported type", 
            "Unsupported index type", 
            "Incompatible key type", 
            "Incompatible value type", 
            "Not enough space", 
            "Database file is corrupted", 
            "Failed to instantiate the object of", 
            "Failed to build descriptor for", 
            "Stub object is accessed", 
            "Invalid object reference", 
            "Access to the deleted object", 
            "Object access violation", 
            "Failed to locate", 
            "Ambiguity definition of class", 
            "Could not find indexed field",
            "No such property",
            "Bad property value",
            "Bad replication prt",
            "Connection failure",
            "Too much objects",
            "Attempt to modify replica",
            "Index key is null",
            "Invalid operation",
            "Database is opened in read-only mode",
            "Not within serializable transaction context",  
            "Wrong cipher key for the encrypted database"
        };
        
        
         
        public StorageError(ErrorCode errorCode):base(messageText[(int)errorCode])
        {
            this.errorCode = errorCode;
        }
        
        public StorageError(ErrorCode errorCode, Exception x):base(messageText[(int)errorCode] + ": " + x)
        {
            this.errorCode = errorCode;
            origEx = x;
        }
        
        public StorageError(ErrorCode errorCode, object param):base(messageText[(int)errorCode] + " " + param)
        {
            this.errorCode = errorCode;
        }
        
        public StorageError(ErrorCode errorCode, object param, System.Exception x):base(messageText[(int)errorCode] + " " + param + ": " + x)
        {
            this.errorCode = errorCode;
            origEx = x;
        }
        
        private ErrorCode errorCode;
        private Exception origEx;
    }
}