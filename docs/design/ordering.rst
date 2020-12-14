Ordering
===========

One of the complications with event sourcing is that ordering needs to be guaranteed for the system
to function correctly. This applies to all kinds of objects:

* Commands need to be executed sequentially 
* Aggregates need to be hydrated in order of versions
* Projections ( especially from multiple streams ) need to be created by timestamp order

In case of single thread the ordering can be trivially guaranteed but as soon as multiple threads are used 
this ordering can be broken.

Optimistic concurrency
**********************

In Event Sourcing each aggregate is rehydrated from the event store every time so the aggregate objects 
are naturally independent objects. Their shared state is the underlying stream ( and its events ) and the 
concurrency is handled via the *Version* property. If multiple commands are executed concurrently then 
after the first one is executed the version would be ahead of the original version expected and an exception
will be thrown.

.. code-block:: csharp

    if (stream.Version >= 0 && es.Version - events.Count < stream.Version)
        throw new InvalidOperationException($"Stream ( {stream.Key}@{stream.Version} ) is ahead of aggregate root with version {es.Version - events.Count} saving {events.Count} events )");

While this allows to handle invalid concurrency the commands would still need to execute sequentially for each 
aggregate. 

Arrival time
************
