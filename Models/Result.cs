using System;

namespace Neo4jClientVector.Models
{
    public enum ResultStatus
    {
        Success,
        Info,
        Warning,
        Error,
        Rejected,
        Unauthorized
    }
    public enum ResultStatusDetail
    {
        ActionAlreadyPerformed,
        NotFound
    }
    public class Result
    {
        public ResultStatus Status { get; set; }
        public ResultStatusDetail StatusDetail { get; set; }
        public Exception Exception { get; set; }
        public string Message { get; set; }
        public string Timestamp { get; set; }

        public bool Successful(Result status)
        {
            return Status == ResultStatus.Success && status.Status == ResultStatus.Success;
        }

        public bool Successful()
        {
            return Status == ResultStatus.Success;
        }

        public bool Successful<T>(Result<T> status)
        {
            return Status == ResultStatus.Success && status.Successful();
        }

        public bool Unsuccessful()
        {
            return !Successful();
        }

        public Result ActionAlreadyPerformed()
        {
            if (Status == ResultStatus.Rejected)
            {
                StatusDetail = ResultStatusDetail.ActionAlreadyPerformed;
            }
            return this;
        }

        public static Result<T> NotFound<T>()
        {
            return new Result<T>
            {
                Status = ResultStatus.Rejected,
                StatusDetail = ResultStatusDetail.NotFound
            };
        }

        public Result NotFound(dynamic data = null)
        {
            if (Status == ResultStatus.Rejected)
            {
                StatusDetail = ResultStatusDetail.NotFound;
            }
            return this;
        }

        public static Result Error(string message = null, Exception exception = null, string timestamp = null)
        {
            return new Result { Status = ResultStatus.Error, Message = message, Exception = exception, Timestamp = timestamp };
        }
        public static Result Success()
        {
            return new Result { Status = ResultStatus.Success };
        }
        public static Result<T> Rejected<T>(T data)
        {
            return new Result<T> { Status = ResultStatus.Rejected, Data = data };
        }
        public static Result<T> Success<T>(T data)
        {
            return new Result<T> { Status = ResultStatus.Success, Data = data };
        }
        public static Result<T> Info<T>(T data = default(T), string message = null)
        {
            return new Result<T> { Status = ResultStatus.Info, Message = message, Data = data };
        }
        public static Result<T> Warning<T>(string message = null, Exception exception = null)
        {
            return new Result<T> { Status = ResultStatus.Warning, Message = message, Exception = exception };
        }
        public static Result<T> Error<T>(string message = null, Exception exception = null, T data = default(T), string timestamp = null)
        {
            return new Result<T> { Status = ResultStatus.Error, Message = message, Exception = exception, Data = data, Timestamp = timestamp };
        }
        public static Result<T> Rejected<T>(string message = null)
        {
            return new Result<T> { Status = ResultStatus.Rejected, Message = message };
        }
        public static Result<T> Rejected<T>(ResultStatusDetail detail, string message = null)
        {
            return new Result<T> { Status = ResultStatus.Rejected, StatusDetail = detail, Message = message };
        }
        public static Result Unauthorized(string message = null)
        {
            return new Result { Status = ResultStatus.Unauthorized, Message = message };
        }
        public static Result<T> Unauthorized<T>(string message = null)
        {
            return new Result<T> { Status = ResultStatus.Unauthorized, Message = message };
        }
        public static Result Rejected(string message = null)
        {
            return new Result { Status = ResultStatus.Rejected, Message = message };
        }
    }
    public class Result<T> : Result
    {
        public T Data { get; set; }
    }
}
