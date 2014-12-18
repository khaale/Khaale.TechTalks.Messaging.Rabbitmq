khaale.techtalks.messaging.rabbitmq
===================================

Contains examples of RabbitMQ usage from .NET.


## Multithreading and concurrency 
`IConnection.CreateModel` can cause deadlocks when it's called simultaniously with `IModel.BasicPublish`. 
(also 
see http://stackoverflow.com/questions/5681118/rabbitmq-channel-creation-guidelines). That's why I suggest the following approaches (but it need further investigation):
* For *non-transactional publishing* consider using *1 publish channel* per connection. Publishing can be performed from multiple treads, but with synchronization. This apporach is used by EasyNetQ.
* For *transactional publishing* consider using *channel's pool* to avoid blocking.
* For *consuming* consider using *multiple channels* created on app initialization
