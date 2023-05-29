namespace ConsoleApp
{
    public class CustomSample
    {
        private readonly string m_Raw;

        public string Raw => m_Raw;

        public CustomSample(string raw)
        {
            m_Raw = raw;
        }
    }
}
