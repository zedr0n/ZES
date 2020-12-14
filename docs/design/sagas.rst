Sagas
=====



Aggregates function as transaction boundaries and have one-to-one correspondence with the underlying streams. 
But there're cases when changes to one aggregate will affect another aggregate so we need some coordinating objects.
These kinds of objects are called sagas ( also known as process managers ).

What does saga do:

* Listen to events
* Change its own state based on event
* Produce commands 

There are multiple ways to implement sagas but the one chosen here is event-sourced state machines. 

