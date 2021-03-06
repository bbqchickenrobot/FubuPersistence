﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using FubuMVC.Core;
using FubuMVC.Core.Ajax;
using FubuMVC.Katana;
using FubuMVC.StructureMap;
using FubuPersistence.RavenDb;
using FubuTestingSupport;
using NUnit.Framework;
using System.Linq;
using Raven.Client;
using StructureMap;


namespace FubuPersistence.Tests.RavenDb.Integration
{
    [TestFixture]
    public class TransactionBehaviorIntegratedTester
    {
        [Test]
        public void posts_are_committed()
        {
            var container = new Container(new RavenDbRegistry());
            container.Inject(new RavenDbSettings{RunInMemory = true});

            using (var application = FubuApplication.For<NamedEntityRegistry>().StructureMap(container).RunEmbedded())
            {
                application.Endpoints.PostJson(new NamedEntity {Name = "Jeremy"}).StatusCode.ShouldEqual(HttpStatusCode.OK);
                application.Endpoints.PostJson(new NamedEntity {Name = "Josh"}).StatusCode.ShouldEqual(HttpStatusCode.OK);
                application.Endpoints.PostJson(new NamedEntity {Name = "Vyrak"}).StatusCode.ShouldEqual(HttpStatusCode.OK);
            
                application.Services.GetInstance<ITransaction>().Execute<IDocumentSession>(session => {
                    session.Query<NamedEntity>()
                           .Customize(x => x.WaitForNonStaleResults())
                           .Each(x => Debug.WriteLine(x.Name));
                });

                application.Endpoints.Get<FakeEntityEndpoint>(x => x.get_names()).ReadAsJson<NamesResponse>()
                    .Names.ShouldHaveTheSameElementsAs("Jeremy", "Josh", "Vyrak");
                    
            }
        }
    }

    public class NamedEntityRegistry : FubuRegistry
    {
        public NamedEntityRegistry()
        {
            Services(x => x.ReplaceService(new RavenDbSettings { RunInMemory = true}));
        }
    }

    public class FakeEntityEndpoint
    {
        private readonly IEntityRepository _repository;

        public FakeEntityEndpoint(IEntityRepository repository)
        {
            _repository = repository;
        }

        public NamesResponse get_names()
        {
            return new NamesResponse
            {
                Names = _repository.All<NamedEntity>().OrderBy(x => x.Name).Select(x => x.Name).ToArray()
            };
        }

        public AjaxContinuation post_name(NamedEntity entity)
        {
            _repository.Update(entity);

            return AjaxContinuation.Successful();
        }
    }

    public class NamesResponse
    {
        public string[] Names { get; set; }
    }

    public class NamedEntity : Entity
    {
        public string Name { get; set; }
    }
}