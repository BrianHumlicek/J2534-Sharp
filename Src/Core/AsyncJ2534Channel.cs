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
using System.Threading;
using System.Threading.Tasks;

namespace SAE.J2534
{
    /// <summary>
    /// Async convenience wrapper around <see cref="J2534Channel"/> for UI responsiveness.
    /// Offloads blocking J2534 hardware calls to the thread pool via Task.Run.
    /// For non-blocking operations (filters, config, clear buffers), use the underlying
    /// <see cref="Channel"/> directly.
    /// </summary>
    public sealed class AsyncJ2534Channel : IAsyncDisposable, IDisposable
    {
        public J2534Channel Channel { get; }

        public AsyncJ2534Channel(J2534Channel channel)
        {
            Channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        /// <summary>
        /// Reads a single message using the default timeout.
        /// </summary>
        public Task<GetMessagesResult> ReadMessageAsync(CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.ReadMessage(), cancellationToken);

        /// <summary>
        /// Reads up to the specified number of messages using the default timeout.
        /// </summary>
        public Task<GetMessagesResult> ReadMessagesAsync(int count, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.ReadMessages(count), cancellationToken);

        /// <summary>
        /// Reads up to the specified number of messages with a custom timeout.
        /// </summary>
        public Task<GetMessagesResult> ReadMessagesAsync(int count, int timeoutMs, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.ReadMessages(count, timeoutMs), cancellationToken);

        /// <summary>
        /// Sends a single message with the default flags and timeout.
        /// </summary>
        public Task<J2534Result> SendMessageAsync(byte[] data, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.SendMessage(data), cancellationToken);

        /// <summary>
        /// Sends a single message with custom flags and default timeout.
        /// </summary>
        public Task<J2534Result> SendMessageAsync(byte[] data, TxFlag txFlags, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.SendMessage(data, txFlags), cancellationToken);

        /// <summary>
        /// Sends a single message with custom flags and timeout.
        /// </summary>
        public Task<J2534Result> SendMessageAsync(byte[] data, TxFlag txFlags, int timeoutMs, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.SendMessage(data, txFlags, timeoutMs), cancellationToken);

        /// <summary>
        /// Sends a message object.
        /// </summary>
        public Task<J2534Result> SendMessageAsync(Message message, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.SendMessage(message), cancellationToken);

        /// <summary>
        /// Sends a message object with custom timeout.
        /// </summary>
        public Task<J2534Result> SendMessageAsync(Message message, int timeoutMs, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.SendMessage(message, timeoutMs), cancellationToken);

        /// <summary>
        /// Sends multiple messages.
        /// </summary>
        public Task<J2534Result> SendMessagesAsync(Message[] messages, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.SendMessages(messages), cancellationToken);

        /// <summary>
        /// Sends multiple messages with custom timeout.
        /// </summary>
        public Task<J2534Result> SendMessagesAsync(Message[] messages, int timeoutMs, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.SendMessages(messages, timeoutMs), cancellationToken);

        /// <summary>
        /// Performs a 5-baud initialization (ISO9141).
        /// </summary>
        public Task<J2534Result<byte[]>> FiveBaudInitAsync(byte targetAddress, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.FiveBaudInit(targetAddress), cancellationToken);

        /// <summary>
        /// Performs a fast initialization (ISO14230).
        /// </summary>
        public Task<J2534Result<Message>> FastInitAsync(Message txMessage, CancellationToken cancellationToken = default) =>
            Task.Run(() => Channel.FastInit(txMessage), cancellationToken);

        public void Dispose() => Channel.Dispose();

        public ValueTask DisposeAsync()
        {
            Channel.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
