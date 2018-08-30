using System;

namespace Detourium
{
    public enum ErrorCode
    {
        /// <summary>
        /// No matching error code was found.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// The plugin name must be compliant with the restrictions provided.
        /// <para>
        /// The plugin name must be unique; alphanumeric characters; no spaces, and up to 80 characters in length.
        /// </para>
        /// </summary>
        InvalidPluginName = 4,

        /// <summary>
        /// The plugin is missing a required property.
        /// </summary>
        MissingProperty = 8,

        /// <summary>
        /// The process was not running at the time during DLL injection.
        /// </summary>
        ProcessNotFound = 16,

        /// <summary>
        /// The module name speified during install is invalid.
        /// </summary>
        InvalidModuleName = 32,

        /// <summary>
        /// The procedure name specified during install is invalid.
        /// </summary>
        InvalidProcName = 64,

        /// <summary>
        /// The virtual protection was unable to be modified during install.
        /// </summary>
        VirtualProtectFailed = 128,
    }

    [Serializable]
    public class DetouriumPluginException : Exception
    {
        /// <summary>
        /// The specific error type of the exception.
        /// </summary>
        public ErrorCode ErrorCode { get; }

        public DetouriumPluginException() { }
        public DetouriumPluginException(string message, ErrorCode errorCode = ErrorCode.Unspecified) : base(message) {
            message += $" (ErrorCode: { Enum.GetName(typeof(ErrorCode), errorCode) })";
            this.ErrorCode = errorCode;
        }

        public DetouriumPluginException(string message) : base(message)
        {
        }

        public DetouriumPluginException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DetouriumPluginException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }
    }
}
