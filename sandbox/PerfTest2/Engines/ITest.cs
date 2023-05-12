using System;

namespace TestPerfLiteDB
{
    public interface ITest : IDisposable
    {
        int Count { get; }
        int FileLength { get; }

        void Prepare();
        void Insert();
        void Bulk();
        void Update();
        void CreateIndex();
        void Query();
        //void Delete();
        //void Drop();
    }
}
