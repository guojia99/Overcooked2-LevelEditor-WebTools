# OC2 LevelEditor Web Tools

> - 起因是因为Unity2017界面不好用，所以就研发了这么一个工具。

### 注意事项
- 注意，在更新版本或者操作文件之前，需要把Unity关闭，以免出现意外错误。
  - 实测丢失了一些数据的。
- 本项目无法保证百分百不会出问题，在使用前请使用测试关卡试用，以免丢失数据。
- 本项目无法代替unity，只是减少了繁琐的unity的操作，最终调试还是需要使用unity。


### 环境准备
- 基于GUA老师的编辑器，请正确安装好GUA老师的项目后再继续
- https://github.com/gua248/Overcooked2-LevelEditor

#### 安装和开始方法
1. 将本仓库下载到你的本地
- https://github.com/guojia99/Overcooked2-LevelEditor-WebTools
2. 将`LayoutEditor` 放到`Overcooked2-LevelEditor/Assets/Editor`中
3. 将`layout-editor` 放到`Overcooked2-LevelEditor/` 目录下即可
4. 点击菜单`Tool`菜单Open Bridge 即可使用
![img.png](img.png)



### 功能清单

- [x] 布局编辑器
  - [x] 地板编辑
  - [x] 物品编辑
  - [x] 背景道具编辑
  - [x] 背景地板编辑
  - [x] 风景编辑
- [x] 菜单编辑器
  - [x] 食材校验
  - [x] 锅具校验
- [x] 关卡编辑器
  - [x] 信息便捷修改
  - [x] 快捷创建关卡
- [ ] 自定义食材管理


- 缺少木筏？
- 背景缺少海水和沙滩
- 传送带需要用一个箭头指明方向， 传送门需要知道能够绑定


### 报告bug
- 你可以通过github 提交issue报告bug
- 或者加入QQ群聊 1091785437

### 更新日志
- 20260730-02:29 完成v0.1.0的基础功能研发
- 20260730-17:45 完成v0.2.0研发，支持了背景、地板管理，以及关卡管理、菜单和食材校验
- 20260730-19:00 完成v0.2.1研发，支持了多角度旋转、核心组件支持界面修改参数。