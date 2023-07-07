using System.Text.Json.Serialization;

namespace ConsoleApp
{
    [JsonDerivedType(typeof(SampleChildren1), typeDiscriminator: "Children1")]
    [JsonDerivedType(typeof(SampleChildren2), typeDiscriminator: "Children2")]
    public class SampleParent
    {
        public string Id;
    }

    public class SampleChildren1 : SampleParent
    {
        public int IntValue1;
    }

    public class SampleChildren2 : SampleParent
    {
        public int IntValue2;
    }
}
