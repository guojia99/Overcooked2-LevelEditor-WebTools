# Binary Reader for JavaScript/TypeScript

[![npm](https://img.shields.io/npm/v/fbx-parser)](https://www.npmjs.com/package/@picode/binary-reader)
![npm bundle size](https://img.shields.io/bundlephobia/minzip/@picode/binary-reader)

Tool for reading binary data sequentially in JavaScript / TypeScript

Requires [DataView.getBigInt64 support](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/DataView/getBigInt64#browser_compatibility) for Int64/Uint64

## Installation

```bash
npm install bin-reader
```

## Usage

```ts
import { BinaryReader } from 'bin-reader'

const data = new BinaryReader(uint8Array)

const version = data.readUint32()
const nameLen = data.readUint8()
const name = data.readArrayAsString(nameLen)
```

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

## License

[MIT](/LICENSE)
