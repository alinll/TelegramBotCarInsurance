namespace TelegramBotCarInsurance
{
    internal class UserDocuments
    {
        public bool PassportReceived { get; set; } = false;

        public bool VehicleDocReceived { get; set; } = false;

        public string WaitingFor { get; set; } = "";

        public string? PassportFileId { get; set; }

        public string? VehicleFileId { get; set; }
    }
}
