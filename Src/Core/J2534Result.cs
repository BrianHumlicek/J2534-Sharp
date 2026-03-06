#region License
/*Copyright(c) 2024, Brian Humlicek
* https://github.com/BrianHumlicek
* 
*Permission is hereby granted, free of charge, to any person obtaining a copy
*of this software and associated documentation files (the "Software"), to deal
*in the Software without restriction, including without limitation the rights
*to use, copy, modify, merge, publish, distribute, sub-license, and/or sell
*copies of the Software, and to permit persons to whom the Software is
*furnished to do so, subject to the following conditions:
*The above copyright notice and this permission notice shall be included in all
*copies or substantial portions of the Software.
*
*THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
*IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
*FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
*AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
*LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
*OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
*SOFTWARE.
*/
#endregion License
using System;
using System.Diagnostics.CodeAnalysis;

namespace SAE.J2534
{
    /// <summary>
    /// Represents the result of a J2534 API operation, containing either a success value or an error.
    /// </summary>
    public readonly struct J2534Result<T>
    {
        private readonly T _value;
        private readonly string? _errorMessage;

        public ResultCode Status { get; }

        [MemberNotNullWhen(true, nameof(_value))]
        [MemberNotNullWhen(false, nameof(_errorMessage))]
        public bool IsSuccess => Status == ResultCode.STATUS_NOERROR;

        public bool IsError => !IsSuccess;

        private J2534Result(ResultCode status, T value, string? errorMessage)
        {
            Status = status;
            _value = value;
            _errorMessage = errorMessage;
        }

        public static J2534Result<T> Success(T value) => 
            new J2534Result<T>(ResultCode.STATUS_NOERROR, value, null);

        public static J2534Result<T> Error(ResultCode status, string? message = null) => 
            new J2534Result<T>(status, default!, message);

        public T Value => IsSuccess ? _value : throw new InvalidOperationException($"Cannot access Value on failed result: {Status}");

        public string ErrorMessage => IsError ? (_errorMessage ?? Status.GetDescription()) : throw new InvalidOperationException("Cannot access ErrorMessage on successful result");

        public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? _value : defaultValue;

        public J2534Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            return IsSuccess 
                ? J2534Result<TNew>.Success(mapper(_value)) 
                : J2534Result<TNew>.Error(Status, _errorMessage);
        }

        public J2534Result<TNew> Bind<TNew>(Func<T, J2534Result<TNew>> binder)
        {
            return IsSuccess 
                ? binder(_value) 
                : J2534Result<TNew>.Error(Status, _errorMessage);
        }

        public void Match(Action<T> onSuccess, Action<ResultCode, string> onError)
        {
            if (IsSuccess)
                onSuccess(_value);
            else
                onError(Status, _errorMessage ?? Status.GetDescription());
        }

        public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<ResultCode, string, TResult> onError)
        {
            return IsSuccess 
                ? onSuccess(_value) 
                : onError(Status, _errorMessage ?? Status.GetDescription());
        }

        public static implicit operator bool(J2534Result<T> result) => result.IsSuccess;

        public override string ToString() => 
            IsSuccess 
                ? $"Success: {_value}" 
                : $"Error ({Status}): {_errorMessage ?? Status.GetDescription()}";
    }

    /// <summary>
    /// Result type for operations that don't return a value (void equivalent).
    /// </summary>
    public readonly struct J2534Result
    {
        private readonly string? _errorMessage;

        public ResultCode Status { get; }

        public bool IsSuccess => Status == ResultCode.STATUS_NOERROR;
        public bool IsError => !IsSuccess;

        private J2534Result(ResultCode status, string? errorMessage)
        {
            Status = status;
            _errorMessage = errorMessage;
        }

        public static J2534Result Success() => 
            new J2534Result(ResultCode.STATUS_NOERROR, null);

        public static J2534Result Error(ResultCode status, string? message = null) => 
            new J2534Result(status, message);

        public string ErrorMessage => IsError ? (_errorMessage ?? Status.GetDescription()) : throw new InvalidOperationException("Cannot access ErrorMessage on successful result");

        public void Match(Action onSuccess, Action<ResultCode, string> onError)
        {
            if (IsSuccess)
                onSuccess();
            else
                onError(Status, _errorMessage ?? Status.GetDescription());
        }

        public TResult Match<TResult>(Func<TResult> onSuccess, Func<ResultCode, string, TResult> onError)
        {
            return IsSuccess 
                ? onSuccess() 
                : onError(Status, _errorMessage ?? Status.GetDescription());
        }

        public static implicit operator bool(J2534Result result) => result.IsSuccess;

        public override string ToString() => 
            IsSuccess 
                ? "Success" 
                : $"Error ({Status}): {_errorMessage ?? Status.GetDescription()}";
    }
}
