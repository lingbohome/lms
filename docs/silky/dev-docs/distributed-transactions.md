---
title: 分布式事务
lang: zh-cn
---

## 概念

在微服务应用(分布式应用中)，完成某一个业务功能可能需要横跨多个服务，操作多个数据库。这就涉及到到了分布式事务，用需要操作的资源位于多个资源服务器上，而应用需要保证对于多个资源服务器的数据的操作，要么全部成功，要么全部失败。本质上来说，分布式事务就是为了保证不同资源服务器的数据一致性。

## 与分布式事务相关的理论

### 经典的分布式系统理论-CAP

1. 一致性

    一致性指**all nodes see the same data at the same time**，即更新操作成功并返回客户端完成后，所有节点在同一时间的数据完全一致，不能存在中间状态。例如对于电商系统用户下单操作，库存减少、用户资金账户扣减、积分增加等操作必须在用户下单操作完成后必须是一致的。不能出现类似于库存已经减少，而用户资金账户尚未扣减，积分也未增加的情况。如果出现了这种情况，那么就认为是不一致的。

    关于一致性，如果的确能像上面描述的那样时刻保证客户端看到的数据都是一致的，那么称之为强一致性。如果允许存在中间状态，只要求经过一段时间后，数据最终是一致的，则称之为最终一致性。此外，如果允许存在部分数据不一致，那么就称之为弱一致性。

2. 可用性

    可用性是指系统提供的服务必须一直处于可用的状态，对于用户的每一个操作请求总是能够在有限的时间内返回结果。**有限的时间内**是指，对于用户的一个操作请求，系统必须能够在指定的时间内返回对应的处理结果，如果超过了这个时间范围，那么系统就被认为是不可用的。试想，如果一个下单操作，为了保证分布式事务的一致性，需要10分钟才能处理完，那么用户显然是无法忍受的。“返回结果”是可用性的另一个非常重要的指标，它要求系统在完成对用户请求的处理后，返回一个正常的响应结果，不论这个结果是成功还是失败。

3. 分区容错性

    分布式系统在遇到任何网络分区故障的时候，仍然需要能够保证对外提供满足一致性和可用性的服务，除非是整个网络环境都发生了故障。


**小结**：既然一个分布式系统无法同时满足一致性、可用性、分区容错性三个特点，我们就需要抛弃一个，需要明确的一点是，对于一个分布式系统而言，分区容错性是一个最基本的要求。因为既然是一个分布式系统，那么分布式系统中的组件必然需要被部署到不同的节点，否则也就无所谓分布式系统了。而对于分布式系统而言，网络问题又是一个必定会出现的异常情况，因此分区容错性也就成为了一个分布式系统必然需要面对和解决的问题。因此系统架构师往往需要把精力花在如何根据业务特点在C（一致性）和A（可用性）之间寻求平衡。而前面我们提到的X/Open XA 两阶段提交协议的分布式事务方案，强调的就是一致性；由于可用性较低，实际应用的并不多。而基于BASE理论的柔性事务，强调的是可用性，目前大行其道，大部分互联网公司采可能会优先采用这种方案。

### BASE理论

eBay的架构师Dan Pritchett源于对大规模分布式系统的实践总结，在ACM上发表文章提出BASE理论。文章链接：https://queue.acm.org/detail.cfm?id=1394128

BASE理论是对CAP理论的延伸，核心思想是即使无法做到强一致性（Strong Consistency，CAP的一致性就是强一致性），但应用可以采用适合的方式达到最终一致性（Eventual Consitency）。    


![distributed-transactions1.png](/assets/imgs/distributed-transactions1.png)

BASE是Basically Available（基本可用）、Soft state（软状态）和Eventually consistent（最终一致性）三个短语的缩写。

1. 基本可用（Basically Available）

    指分布式系统在出现不可预知故障的时候，允许损失部分可用性。

2. 软状态（ Soft State）

    指允许系统中的数据存在中间状态，并认为该中间状态的存在不会影响系统的整体可用性。

3. 最终一致（ Eventual Consistency）

    强调的是所有的数据更新操作，在经过一段时间的同步之后，最终都能够达到一个一致的状态。因此，最终一致性的本质是需要系统保证最终数据能够达到一致，而不需要实时保证系统数据的强一致性。

BASE理论面向的是大型高可用可扩展的分布式系统，和传统的事物ACID特性是相反的。它完全不同于ACID的强一致性模型，而是通过牺牲强一致性来获得可用性，并允许数据在一段时间内是不一致的，但最终达到一致状态。但同时，在实际的分布式场景中，不同业务单元和组件对数据一致性的要求是不同的，因此在具体的分布式系统架构设计过程中，ACID特性和BASE理论往往又会结合在一起。


典型的柔性事务方案：

1. 最大努力通知（非可靠消息、定期校对）

2. 可靠消息最终一致性（异步确保型）

3. TCC（两阶段型、补偿型）

## Silky框架分布式事务的实现方式

silky框架的分布式事务解决方案采用的TCC事务模型实现了分布式事务的最终一致性。在开发过程中参考和借鉴了[hmily](https://github.com/dromara/hmily)。使用AOP的编程思想,在rpc通信过程中通过拦截器的方式对全局事务或是分支事务进行管理和协调。

## 如何使用

在一个分布式事务中,参与分布式事务的方法可能存在多个,在使用过程中,对**每个**参与分布式事务的方法的写法都一致。

1. 在需要参与分布式事务的应用服务接口中,通过特性`TransactionAttribute`进行标注。

```csharp
[Transaction]
Task<string> Delete(string name);
```

2. 应用服务接口实现方法通过`TccTransactionAttribute`特性标注，并通过参数`ConfirmMethod`、`CancelMethod`指定确认、取消步骤时执行的方法。

```csharp
[TccTransaction(ConfirmMethod = "DeleteConfirm", CancelMethod = "DeleteCancel")]
public async Task<string> Delete(string name)
{
    await _anotherAppService.DeleteOne(name);
    await _anotherAppService.DeleteTwo(name);
    return name + " v1";
}

// 分布式事务Comfirm时执行的方法
public async Task<string> DeleteConfirm(string name)
{
    return name + " DeleteConfirm v1";
}

// 分布式事务Cancel时执行的方法
public async Task<string> DeleteCancel(string name)
{
    return name + "DeleteConcel v1";
}

```

关于分布式事务的更详细用法,开发者可以参考[silky框架分布式事务使用简介](/blog/silky-sample-order.md)。

::: warning

指定的`ConfirmMethod`、`CancelMethod`方法声明必须为`public`,输入参数与应用服务接口定义的参数一致。

:::
