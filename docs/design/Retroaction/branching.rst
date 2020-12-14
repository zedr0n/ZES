Branching
---------

We also introduce the concept of timeline ( or branch ) for each of the streams, the stream key is of format

.. code-block::

    {timeline}:{type}:{id}

Each of the streams descriptors contains a link to the parent stream

.. doxygeninterface:: ZES::Interfaces::EventStore::IStream
    :members: Parent, Key, Version
    :outline:

When hydrating an aggregate from the stream the parent stream events are concatenated using *Parent.Version*
forming a continuous set of events up to latest version.

There's a special service :ref:`IBranchManager` responsible for branch operations

Branch
======

Similar to Git we can create a new branch at any particular point 

.. doxygeninterface:: ZES::Interfaces::Branching::IBranchManager
    :members: Branch

The active branch will be the newly created branch after the operation is completed

Merge
=====

Any branch can be merged to active timeline but only in fast-forward way. So each of the streams needs to be 
a subset of the corresponding stream on the branch which is merged from

.. doxygeninterface:: ZES::Interfaces::Branching::IBranchManager
    :members: Merge
