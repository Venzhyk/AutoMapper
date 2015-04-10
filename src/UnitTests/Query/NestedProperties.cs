using System.Linq;
using AutoMapper.QueryableExtensions;
using Should;
using Xunit;

namespace AutoMapper.UnitTests.Query
{

    public class NestedProperties : AutoMapperSpecBase
    {
        private Dest[] _destList;

        class Source
        {
            public SourceChild Child1 { get; set; }
            public SourceChild Child2 { get; set; }
        }

        class SourceChild
        {


            public int Value { get; set; }
        }

        class Dest
        {
            public DestChild Child1 { get; set; }
            public DestChild Child2 { get; set; }
        }

        class DestChild
        {
            public DestChild(int value)
            {
                Value = value;
            }
            public int Value { get; set; }
        }

        protected override void Establish_context()
        {
            Mapper.CreateMap<SourceChild, DestChild>().ReverseMap();
            Mapper.CreateMap<Source, Dest>().ReverseMap();

       }

        protected override void Because_of()
        {
            _destList = new[]
            {
                new Dest
                {
                    Child1 = new DestChild(10),
                    Child2 = new DestChild(1000),
                }, new Dest
                {
                    Child1 = new DestChild(200),
                    Child2 = new DestChild(500),
                }
            };

          
        }

        [Fact(Skip="Expression mapper bug. It cannot convert nested properties properly")]
        public void Should_filtrate_by_nested_properties()
        {
            var dests = new Source[0].AsQueryable()
             .Where(s => s.Child1.Value > 100 || s.Child2.Value < 1000)
             .Map<Source, Dest>(_destList.AsQueryable());

            dests.Count().ShouldEqual(1);
            dests.First().Child1.Value.ShouldEqual(200);
            dests.First().Child2.Value.ShouldEqual(500);
        }
    }

}
