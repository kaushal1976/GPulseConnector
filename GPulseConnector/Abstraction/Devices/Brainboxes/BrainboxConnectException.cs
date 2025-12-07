namespace GPulseConnector.Abstraction.Devices.Brainboxes
{
    public sealed class BrainboxConnectException : Exception
    {
        public BrainboxConnectException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}