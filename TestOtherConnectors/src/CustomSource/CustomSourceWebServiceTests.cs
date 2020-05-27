using ALE.ETLBox;
using ALE.ETLBox.ConnectionManager;
using ALE.ETLBox.ControlFlow;
using ALE.ETLBox.DataFlow;
using ALE.ETLBox.Helper;
using ALE.ETLBox.Logging;
using ALE.ETLBoxTests.Fixtures;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ALE.ETLBoxTests.DataFlowTests
{
    [Collection("DataFlow")]
    public class CustomSourceWebServiceTests
    {
        public SqlConnectionManager Connection => Config.SqlConnection.ConnectionManager("DataFlow");
        public CustomSourceWebServiceTests(DataFlowDatabaseFixture dbFixture)
        {
        }

        /// <summary>
        /// See https://jsonplaceholder.typicode.com/ for details of the rest api
        /// used for this test
        /// </summary>
        [Fact]
        public void CustomSourceWithWebService()
        {
            //Arrange
            SqlTask.ExecuteNonQuery(Connection, "Create test table",
                @"CREATE TABLE dbo.WebServiceDestination 
                ( Id INT NOT NULL, Title NVARCHAR(100) NOT NULL, Completed BIT NOT NULL )"
            );
            DbDestination<Todo> dest = new DbDestination<Todo>(Connection, "dbo.WebServiceDestination");
            WebserviceReader wsreader = new WebserviceReader();

            //Act
            CustomSource<Todo> source = new CustomSource<Todo>(wsreader.ReadTodo, wsreader.EndOfData);
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(5, RowCountTask.Count(Connection, "dbo.WebServiceDestination"));

        }

        public class Todo
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public bool Completed { get; set; }
        }

        public class WebserviceReader
        {
            public string Json { get; set; }
            public int TodoCounter { get; set; } = 1;
            HttpClient httpClient;

            public Todo ReadTodo()
            {
                using (httpClient = MoqWebservice(TodoCounter))
                {
                    var todo = new Todo();
                    var uri = new Uri("https://todos/" + TodoCounter);
                    TodoCounter++;
                    var response = httpClient.GetStringAsync(uri).Result;
                    Newtonsoft.Json.JsonConvert.PopulateObject(response, todo);
                    return todo;
                }
            }

            public bool EndOfData()
            {
                return TodoCounter > 5;
            }

            public HttpClient MoqWebservice(int todoCounter)
            {
                var handlerMock = new Mock<HttpMessageHandler>();
                handlerMock
                   .Protected()
                   // Setup the PROTECTED method to mock
                   .Setup<Task<HttpResponseMessage>>(
                      "SendAsync",
                      ItExpr.IsAny<HttpRequestMessage>(),
                      ItExpr.IsAny<CancellationToken>()
                   )
                   // prepare the expected response of the mocked http call
                   .ReturnsAsync(new HttpResponseMessage()
                   {
                       StatusCode = HttpStatusCode.OK,
                       Content = new StringContent(@"{ 'Id':"+todoCounter+", 'Title':'Test', Completed: 'false' }"),
                   })
                   .Verifiable();
                return new HttpClient(handlerMock.Object);
            }
        }


    }
}
