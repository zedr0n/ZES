Snapshots
=========

While event sourcing allows greater flexibility than storing just the current state this comes also at a performance cost. Getting
the current state for an aggregate requires reading the whole stream from the beginning. This involves JSON deserialization
which can become expensive.

To reduce the cost of hydrating the aggregates we can introduce the snapshot events which contain the whole state of the aggregate.
This is done by introducing a special kind of event:

.. doxygeninterface:: ZES::Interfaces::ISnapshotEvent

The latest snapshot version is tracked using the aggregate *SnapshotVersion* property

.. doxygenclass:: ZES::Infrastructure::Domain::AggregateRoot
    :members: SnapshotVersion
    :outline:

We enrich the :ref:`IStream` descriptor with the SnapshotVersion property which is populated when the aggregate
is persisted

.. code-block:: csharp

    var snapshotVersion = stream.SnapshotVersion;
    var snapshotEvent = events?.LastOrDefault(e => e is ISnapshotEvent);
    if (snapshotEvent != default && snapshotEvent.Version > snapshotVersion)
        snapshotVersion = snapshotEvent.Version;

    if (snapshotVersion > stream.SnapshotVersion)
        stream.SnapshotVersion = snapshotVersion;

This information is then used when hydrating the aggregate

.. code-block:: csharp

    var start = stream.SnapshotVersion;
    var events = await _eventStore.ReadStream<IEvent>(stream, start).ToList();
    if (events.Count == 0)
        return null;
    es = EventSourced.Create<T>(id, start - 1);
    es.LoadFrom<T>(events, computeHash);
    
        
    