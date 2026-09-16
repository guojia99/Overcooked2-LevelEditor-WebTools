using OrderController;

public delegate ServerOrderControllerBase ServerOrderControllerBuilder(VoidGeneric<OrderID> _addedCallback, VoidGeneric<OrderID> _timeoutCallback);
