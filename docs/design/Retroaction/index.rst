Retroactive computations
^^^^^^^^^^^^^^^^^^^^^^^^

* :ref:`Overview`
* :ref:`Branching`

Overview
--------

One of the benefits of event sourcing is that that streams are append-only which is a powerful tool of 
simplification but what loses some flexibility.

One close concept to event sourcing is source control, for example Git, where commits are also append-only 
and only deltas are stored which allows to have the whole history available.

But source control also introduces concepts extending this behaviour:

* Separate branches each of which can have common history but diverge from some point
* Splitting the history by cloning it up to a certain point in history
* Merging the branches to incorporate the changes into other histories

.. include:: branching.rst