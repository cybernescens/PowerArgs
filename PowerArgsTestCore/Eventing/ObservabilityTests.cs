﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs.Cli;
using System.Collections.Generic;
using PowerArgs;

namespace ArgsTests.CLI.Observability
{
    [TestClass]
    [TestCategory(Categories.Eventing)]
    public class ObservabilityTests
    {
        public class SomeOtherObservable : ObservableObject
        {
            public string Name { get { return Get<string>(); } set { Set(value); } }
        }

        public class SomeObservable : ObservableObject
        {
            public Event SomeEvent { get; private set; } = new Event();
            public Event<string> SomeEventWithAString { get; private set; } = new Event<string>();

            public ObservableCollection<string> Strings { get; private set; } = new ObservableCollection<string>();

            public ObservableCollection<SomeOtherObservable> Children{ get; private set; } = new ObservableCollection<SomeOtherObservable>();


            public string Name { get { return Get<string>(); } set { Set(value); } }
            public int Number{ get { return Get<int>(); } set { Set(value); } }
        }

        [TestMethod]
        public void SubscribeUnmanagedToProperty()
        {
            var observable = new SomeObservable();

            var triggerCount = 0;

            using (var subscription = observable.SubscribeUnmanaged(nameof(SomeObservable.Name), () => { triggerCount++;  }))
            {
                Assert.AreEqual(0, triggerCount);
                observable.Name = "Some value";
                Assert.AreEqual(1, triggerCount);
            }

            observable.Name = "Some new value";
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeForLifetimeToProperty()
        {
            var observable = new SomeObservable();

            var triggerCount = 0;

            using (Lifetime lifetime = new Lifetime())
            {
                observable.SubscribeForLifetime(nameof(SomeObservable.Name), () =>
                {
                    triggerCount++;
                }, lifetime);

                Assert.AreEqual(0, triggerCount);
                observable.Name = "Some value";
                Assert.AreEqual(1, triggerCount);
            }

            observable.Name = "Some new value";
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeToProperty()
        {
            var observable = new SomeObservable();

            var triggerCount = 0;

            using (var lifetime = new Lifetime())
            {

                observable.SubscribeForLifetime(nameof(SomeObservable.Name), () => 
                {
                    triggerCount++; 
                }, lifetime);

                Assert.AreEqual(0, triggerCount);
                observable.Name = "Some value";
                Assert.AreEqual(1, triggerCount);
            }

            observable.Name = "Some new value again";
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeOnceToProperty()
        {
            var observable = new SomeObservable();
            var triggerCount = 0;
            observable.SubscribeOnce(nameof(SomeObservable.Name), () => { triggerCount++; });
            Assert.AreEqual(0, triggerCount);
            observable.Name = "Some value";
            Assert.AreEqual(1, triggerCount);
            observable.Name = "Some new value again";
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeToAllProperties()
        {
            var observable = new SomeObservable();
            int numChanged = 0;

            using (var lifetime = new Lifetime())
            {
                observable.SubscribeForLifetime(ObservableObject.AnyProperty, () => { numChanged++; }, lifetime);

                Assert.AreEqual(0, numChanged);
                observable.Name = "Foo";
                Assert.AreEqual(1, numChanged);
                observable.Number = 1;
                Assert.AreEqual(2, numChanged);
            }

            Assert.AreEqual(2, numChanged);
            observable.Name = "Foo2";
            Assert.AreEqual(2, numChanged);
            observable.Number = 2;
            Assert.AreEqual(2, numChanged);
        }

        [TestMethod]
        public void SubscribeUnmanagedToEvent()
        {
            var observable = new SomeObservable();

            var triggerCount = 0;

            using (var subscription = observable.SomeEvent.SubscribeUnmanaged(() => { triggerCount++; }))
            {
                Assert.AreEqual(0, triggerCount);
                observable.SomeEvent.Fire();
                Assert.AreEqual(1, triggerCount);
            }

            observable.SomeEvent.Fire();
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeUnmanagedToEventWithUnsubscribe()
        {
            var observable = new SomeObservable();

            var triggerCount = 0;

            Action handler = () => { triggerCount++; };
            observable.SomeEvent.SubscribeUnmanaged(handler);
            
            Assert.AreEqual(0, triggerCount);
            observable.SomeEvent.Fire();
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeUnmanagedToEventOfStringWithUnsubscribe()
        {
            var observable = new SomeObservable();
            var triggerCount = 0;
            var sub = observable.SomeEventWithAString.SubscribeUnmanaged((s) => { triggerCount++; });

            Assert.AreEqual(0, triggerCount);
            observable.SomeEventWithAString.Fire("Foo");
            Assert.AreEqual(1, triggerCount);

            sub.Dispose();
            observable.SomeEventWithAString.Fire("Foo");
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeForLifetimeToEvent()
        {
            var observable = new SomeObservable();

            var triggerCount = 0;

            using (var lifetime = new Lifetime())
            {
                observable.SomeEvent.SubscribeForLifetime(() => { triggerCount++; }, lifetime);

                Assert.AreEqual(0, triggerCount);
                observable.SomeEvent.Fire();
                Assert.AreEqual(1, triggerCount);
            }

            observable.SomeEvent.Fire();
            Assert.AreEqual(1, triggerCount);
        }

        [TestMethod]
        public void SubscribeToEvent()
        {
            var observable = new SomeObservable();

            var triggerCount = 0;

            using (var lifetime = new Lifetime())
            {

                observable.SomeEvent.SubscribeForLifetime(() => { triggerCount++; }, lifetime);

                Assert.AreEqual(0, triggerCount);
                observable.SomeEvent.Fire();
                Assert.AreEqual(1, triggerCount);


                observable.SomeEvent.Fire();
                Assert.AreEqual(2, triggerCount);
            }

            observable.SomeEvent.Fire();
            Assert.AreEqual(2, triggerCount);
        }

        [TestMethod]
        public void SynchronizeCollection()
        {
            var observable = new SomeObservable();
            int addCalls = 0, removeCalls = 0, changedCalls = 0;

            observable.Strings.Add("a");
            observable.Strings.Add("b");

            using (var lifetime = new Lifetime())
            {
                observable.Strings.SynchronizeForLifetime((s) => { addCalls++; }, (s) => { removeCalls++; }, () => { changedCalls++; }, lifetime);

                Assert.AreEqual(2, addCalls);
                Assert.AreEqual(0, removeCalls);
                Assert.AreEqual(1, changedCalls);

                observable.Strings.Add("c");
                Assert.AreEqual(3, addCalls);
                Assert.AreEqual(0, removeCalls);
                Assert.AreEqual(2, changedCalls);

                observable.Strings.Remove("a");
                Assert.AreEqual(3, addCalls);
                Assert.AreEqual(1, removeCalls);
                Assert.AreEqual(3, changedCalls);
            }

            observable.Strings.Add("d");
            observable.Strings.Remove("d");
            Assert.AreEqual(3, addCalls);
            Assert.AreEqual(1, removeCalls);
            Assert.AreEqual(3, changedCalls);
        }

        [TestMethod]
        public void SubscribeToChildren()
        {
            var observable = new SomeObservable();
            var existinChild = new SomeOtherObservable();
            observable.Children.Add(existinChild);

            int numChildrenChanged = 0;
            int numChildrenAdded = 0;
            int numChildrenRemoved = 0;
            observable.Children.SynchronizeForLifetime(
                (c) =>
                {
                    c.SynchronizeForLifetime(nameof(SomeOtherObservable.Name), () => 
                    {
                        numChildrenChanged++;
                    }
                    , observable.Children.GetMembershipLifetime(c));
                    numChildrenAdded++;
                }, 
                (c) =>
                {
                    numChildrenRemoved++;
                }, 
                () =>
                {
                }, observable);

            var newItem = new SomeOtherObservable();

            observable.Children.Add(newItem);

            Assert.AreEqual(2, numChildrenChanged);
            existinChild.Name = "Change";
            Assert.AreEqual(3, numChildrenChanged);

            newItem.Name = "Second change";
            Assert.AreEqual(4, numChildrenChanged);

            observable.Children.Remove(existinChild);
            existinChild.Name = "Ignored change";
            Assert.AreEqual(4, numChildrenChanged);

            observable.Children.Remove(newItem);
            newItem.Name = "Ignored change";
            Assert.AreEqual(4, numChildrenChanged);

            Assert.AreEqual(2, numChildrenAdded);
            Assert.AreEqual(2, numChildrenRemoved);
        }


        [TestMethod]
        public void TestSubscribeOnce()
        {
            var ev = new Event();
            var counter = 0;
            ev.SubscribeOnce(() => counter++);
            Assert.IsTrue(ev.HasSubscriptions);
            Assert.AreEqual(0, counter);
            ev.Fire();
            Assert.IsFalse(ev.HasSubscriptions);
            Assert.AreEqual(1, counter);
            ev.Fire();
            Assert.AreEqual(1, counter);
        }

        [TestMethod]
        public void TestCollectionIndexAssignmentBehavior()
        {
            var collection = new ObservableCollection<string>();
            collection.Add("Hello");

            var asserted = false;
            collection.AssignedToIndex.SubscribeOnce((assignment) =>
            {
                Assert.AreEqual("Hello", assignment.OldValue);
                Assert.AreEqual("Goodbye", assignment.NewValue);
                asserted = true;
            });
            collection[0] = "Goodbye";
            Assert.IsTrue(asserted);
        }
    }
}
