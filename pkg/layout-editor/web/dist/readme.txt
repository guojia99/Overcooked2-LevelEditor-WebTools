OC2DIYLevelRuntimeWLoader
==========================

依赖包说明
----------

本目录是 Overcooked! 2 自定义关卡运行时依赖包。请将本目录整体放入：

  Overcooked! 2/BepInEx/plugins/OC2DIYLevelRuntimeWLoader/

当前版本
--------

  Loader.dll    v3.0.0
  debugLog.dll  v1.0.0

主要功能
--------

Loader.dll：

  - 在游戏启动阶段加载 commonW1、commonW2 和 webcustomstub_runtime。
  - 注入统一的 WebCustomStubRuntime 程序集。
  - 扫描 OC2DIYLevel/levels 下实际存在的 *_custom_runtime。
  - 普通地图没有 runtime 时不执行额外的 runtime 加载。
  - 场景切换时使用启动扫描结果，不重复遍历全部地图集目录。
  - 对 requires.txt 做版本门控。
  - 记录 runtime 加载失败、程序集解析失败和同名程序集冲突。

debugLog.dll：

  - 捕捉游戏运行中的 Warning、Error、Exception 和 Assert。
  - 默认忽略普通 Debug.Log，减少日志量和性能影响。
  - 日志在后台线程批量写入，不在 Unity 日志回调中直接写磁盘。
  - 相同消息在短时间内自动去重，避免异常刷屏拖慢游戏。
  - 日志文件写入失败不会阻止游戏运行。

目录结构
--------

  Loader.dll
  debugLog.dll
  log_config.txt
  readme.txt
  webcustomstub_runtime
  commonW1
  commonW2              可选，只有使用汉堡等内容时需要

  Loader.dll、debugLog.dll、log_config.txt 必须位于同一目录。

日志文件
--------

debugLog.dll 默认将日志写入自身同级目录：

  debug.log

日志配置 log_config.txt
-------------------------

配置文件必须命名为 log_config.txt，并放在 debugLog.dll 同级目录。
配置采用 xxx=yyy 格式，# 开头为说明。日志默认写入 logs/logs_info_启动时间/，保留最近 21 次启动，
并额外生成 info.log、debug.log、player.log 和 BepInExDebugLog.log。
修改配置后重启游戏生效。配置文件不存在或格式错误时，插件使用内置默认值。

默认配置：

  {
    "enabled": true,
    "level": "warning",
   "fileName": "debug.log",
   "captureUnityLog": true,
   "captureFps": false,
   "captureStackTrace": true,
    "maxQueue": 512,
    "deduplicateSeconds": 5
  }

配置项说明：

  enabled
    类型：true / false
    默认：true
    是否启用 debugLog.dll。设为 false 后不创建后台写日志线程，也不订阅 Unity 日志。

  level
    类型：debug / info / warning
    默认：warning
    日志级别：
      warning：记录 Warning、Error、Exception、Assert。
      info：记录普通 Log，同时记录 warning 级别日志。
      debug：当前版本等同于 info，预留给后续更详细诊断日志。
    推荐：正常游戏保持 warning；需要排查加载流程时使用 info。

  fileName
    类型：字符串
    默认：debug.log
    输出文件名。相对路径相对于 debugLog.dll 所在目录。
    建议只使用文件名，例如 debug.log 或 debug_session_01.log。

  captureUnityLog
    类型：true / false
    默认：true
    是否捕捉 Unity 的 Application.logMessageReceivedThreaded 日志。
    设为 false 后 debugLog.dll 仍可加载，但不会捕捉 Unity 日志。

  captureFps
    类型：true / false
    默认：false
    是否每秒记录一次实际 FPS、该秒最低/最高 FPS 和平均帧耗时。
    只采样和记录，不修改游戏帧率、VSync 或 CustomStub 调度频率。

  captureStackTrace
    类型：true / false
    默认：true
    是否把 Unity 提供的堆栈写入 debug.log。
    排查异常建议保持 true；只看错误摘要时可以设为 false。

  maxQueue
    类型：整数
    默认：512
    后台写入队列的最大条数。队列满时丢弃最旧日志，避免异常刷屏占满内存。
    建议范围：256 到 2048。

  deduplicateSeconds
    类型：整数，单位为秒
    默认：5
    相同消息在该时间内只记录一次。设为 0 表示关闭相同消息去重。
    建议正常保持 5；排查高频重复异常时可以临时设为 0。

推荐使用方式
------------

1. 正常游玩：保持默认配置，日志量最低。
2. 排查地图或 runtime 加载：将 level 改为 info，重启游戏后复现一次。
3. 排查具体异常堆栈：保持 level=warning 和 captureStackTrace=true。
4. 排查异常频率：将 deduplicateSeconds 临时改为 0，但只建议短时间使用。
5. 排查结束后恢复 warning，避免长期记录普通游戏日志。

注意事项
--------

  - 不要删除或改名 Loader.dll、webcustomstub_runtime 和 commonW1。
  - 新版 Loader 只识别 *_custom_runtime，不识别旧版裸 runtime 文件。
  - 如果地图集带 requires.txt，Loader 版本必须满足其中的最低版本要求。
  - 多个 runtime 使用相同程序集名时，先加载的程序集会被保留，后加载版本会被跳过并记录冲突。
  - debug.log 可以在游戏退出后提交给开发者，用于分析加载失败、异常和性能问题。
