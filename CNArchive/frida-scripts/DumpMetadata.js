const PACKAGE_NAME = "";
const LOAD_METADATA_ADDR_BIAS = 0x00;

const Il2CppGlobalMetadataHeader = {
    sanity: 'int32',
    version: 'int32',
    stringLiteralOffset: 'int32',
    stringLiteralSize: 'int32',
    stringLiteralDataOffset: 'int32',
    stringLiteralDataSize: 'int32',
    stringOffset: 'int32',
    stringSize: 'int32',
    eventsOffset: 'int32',
    eventsSize: 'int32',
    propertiesOffset: 'int32',
    propertiesSize: 'int32',
    methodsOffset: 'int32',
    methodsSize: 'int32',
    parameterDefaultValuesOffset: 'int32',
    parameterDefaultValuesSize: 'int32',
    fieldDefaultValuesOffset: 'int32',
    fieldDefaultValuesSize: 'int32',
    fieldAndParameterDefaultValueDataOffset: 'int32',
    fieldAndParameterDefaultValueDataSize: 'int32',
    fieldMarshaledSizesOffset: 'int32',
    fieldMarshaledSizesSize: 'int32',
    parametersOffset: 'int32',
    parametersSize: 'int32',
    fieldsOffset: 'int32',
    fieldsSize: 'int32',
    genericParametersOffset: 'int32',
    genericParametersSize: 'int32',
    genericParameterConstraintsOffset: 'int32',
    genericParameterConstraintsSize: 'int32',
    genericContainersOffset: 'int32',
    genericContainersSize: 'int32',
    nestedTypesOffset: 'int32',
    nestedTypesSize: 'int32',
    interfacesOffset: 'int32',
    interfacesSize: 'int32',
    vtableMethodsOffset: 'int32',
    vtableMethodsSize: 'int32',
    interfaceOffsetsOffset: 'int32',
    interfaceOffsetsSize: 'int32',
    typeDefinitionsOffset: 'int32',
    typeDefinitionsSize: 'int32',
    imagesOffset: 'int32',
    imagesSize: 'int32',
    assembliesOffset: 'int32',
    assembliesSize: 'int32',
    fieldRefsOffset: 'int32',
    fieldRefsSize: 'int32',
    referencedAssembliesOffset: 'int32',
    referencedAssembliesSize: 'int32',
    attributeDataOffset: 'int32',
    attributeDataSize: 'int32',
    attributeDataRangeOffset: 'int32',
    attributeDataRangeSize: 'int32',
    unresolvedIndirectCallParameterTypesOffset: 'int32',
    unresolvedIndirectCallParameterTypesSize: 'int32',
    unresolvedIndirectCallParameterRangesOffset: 'int32',
    unresolvedIndirectCallParameterRangesSize: 'int32',
    windowsRuntimeTypeNamesOffset: 'int32',
    windowsRuntimeTypeNamesSize: 'int32',
    windowsRuntimeStringsOffset: 'int32',
    windowsRuntimeStringsSize: 'int32',
    exportedTypeDefinitionsOffset: 'int32',
    exportedTypeDefinitionsSize: 'int32'
};

var isDumped = false;
const HEADER_SIZE = Object.keys(Il2CppGlobalMetadataHeader).length * 4; // 每个字段4字节

// 监听并等待 libil2cpp.so 加载
function waitForModule(moduleName, callback) {
    // 1. 如果是 attach 模式，此时可能已经加载了，直接回调
    var module = Process.findModuleByName(moduleName);
    if (module) {
        callback(module);
        return;
    }

    // 2. 如果是 spawn 模式，Hook dlopen/android_dlopen_ext 等待加载
    const dlopen = Module.findExportByName(null, "dlopen");
    const android_dlopen_ext = Module.findExportByName(null, "android_dlopen_ext");

    function hookDlopen(addr) {
        if (!addr) return;
        Interceptor.attach(addr, {
            onEnter: function (args) {
                this.path = args[0].readUtf8String();
            },
            onLeave: function (retval) {
                if (retval.isNull()) return;
                // 如果发现加载的是我们需要的目标库
                if (this.path && this.path.indexOf(moduleName) !== -1) {
                    if (!isDumped) {
                        console.log(`\n[+] 动态监听到模块加载: ${this.path}`);
                        // 稍微延迟一点点，确保库在内存中完全初始化
                        setTimeout(() => {
                            callback(Process.findModuleByName(moduleName));
                        }, 100);
                    }
                }
            }
        });
    }

    hookDlopen(dlopen);
    hookDlopen(android_dlopen_ext);
}

function dump_global_metadata() {
    var module = Process.findModuleByName("libil2cpp.so");
    const LOAD_METADATA_ADDR_BIAS = LOAD_METADATA_ADDR_BIAS;
    const LOAD_METADATA_ADDR = module.base.add(LOAD_METADATA_ADDR_BIAS);
    console.log(`>>>>>>>>>>>>>>>>>>>>>> attach LOAD_METADATA: ${LOAD_METADATA_ADDR} >>>>>>>>>>>>>>>>>>>>>>>>>`);
    Interceptor.attach(LOAD_METADATA_ADDR, {
        onEnter:function(args){
            this.loaded = true
        },
        onLeave: function(retval) {
            // 这个retval其实就是文件句柄指针
            if(this.loaded) {
                const header = parseMetadataHeader(retval, Il2CppGlobalMetadataHeader);
                const fileSize = calculateMetadataSize(header);
                const buffer = retval.readByteArray(fileSize);
                
                const path = `/data/data/${PACKAGE_NAME}/global-metadata-decrypted.dat`;
 
                new File(path, "wb").write(buffer);
                console.log(`数据已保存: ${path}`);
            }
        }
    })
}

function parseMetadataHeader(address) {
    let header = {};
    let offset = 0;
    for (const [field, type] of Object.entries(Il2CppGlobalMetadataHeader)) {
        if (type === 'int32') {
            header[field] = address.add(offset).readS32();
        } else {
            // 如果有其他类型，需要扩展
            console.error(`Unsupported type: ${type} for field ${field}`);
            header[field] = 0;
        }
        offset += 4;
    }
    return header;
}

function calculateMetadataSize(header) {
    let maxEnd = 0;
     
    // 检查所有数据段
    const segments = [
        [header.stringLiteralOffset, header.stringLiteralSize],
        [header.stringLiteralDataOffset, header.stringLiteralDataSize],
        [header.stringOffset, header.stringSize],
        [header.eventsOffset, header.eventsSize],
        [header.propertiesOffset, header.propertiesSize],
        [header.methodsOffset, header.methodsSize],
        [header.parameterDefaultValuesOffset, header.parameterDefaultValuesSize],
        [header.fieldDefaultValuesOffset, header.fieldDefaultValuesSize],
        [header.fieldAndParameterDefaultValueDataOffset, header.fieldAndParameterDefaultValueDataSize],
        [header.fieldMarshaledSizesOffset, header.fieldMarshaledSizesSize],
        [header.parametersOffset, header.parametersSize],
        [header.fieldsOffset, header.fieldsSize],
        [header.genericParametersOffset, header.genericParametersSize],
        [header.genericParameterConstraintsOffset, header.genericParameterConstraintsSize],
        [header.genericContainersOffset, header.genericContainersSize],
        [header.nestedTypesOffset, header.nestedTypesSize],
        [header.interfacesOffset, header.interfacesSize],
        [header.vtableMethodsOffset, header.vtableMethodsSize],
        [header.interfaceOffsetsOffset, header.interfaceOffsetsSize],
        [header.typeDefinitionsOffset, header.typeDefinitionsSize],
        [header.imagesOffset, header.imagesSize],
        [header.assembliesOffset, header.assembliesSize],
        [header.fieldRefsOffset, header.fieldRefsSize],
        [header.referencedAssembliesOffset, header.referencedAssembliesSize],
        [header.attributeDataOffset, header.attributeDataSize],
        [header.attributeDataRangeOffset, header.attributeDataRangeSize],
        [header.unresolvedIndirectCallParameterTypesOffset, header.unresolvedIndirectCallParameterTypesSize],
        [header.unresolvedIndirectCallParameterRangesOffset, header.unresolvedIndirectCallParameterRangesSize],
        [header.windowsRuntimeTypeNamesOffset, header.windowsRuntimeTypeNamesSize],
        [header.windowsRuntimeStringsOffset, header.windowsRuntimeStringsSize],
        [header.exportedTypeDefinitionsOffset, header.exportedTypeDefinitionsSize]
    ];
     
    for (const [offset, size] of segments) {
        // 跳过无效段
        if (offset === 0 || size === 0) continue;
         
        const end = offset + size;
        if (end > maxEnd) maxEnd = end;
    }
     
    // 确保最小大小
    if (maxEnd < HEADER_SIZE) {
        console.log(`[!] 警告: 计算大小(${maxEnd})小于头部大小(${HEADER_SIZE})`);
        maxEnd = HEADER_SIZE;
    }
     
    // 向上对齐到4KB 这个可选 我发现不补齐的话和原版的大小是一样的
    // const alignedSize = (maxEnd + 0xFFF) & ~0xFFF;
    // console.log(`[+] 计算大小: ${maxEnd} 字节 (对齐到 ${alignedSize} 字节)`);
     
    return maxEnd;
}

setImmediate(() => {
    console.log("[*] 脚本已注入，等待 libil2cpp.so 加载...");
    waitForModule("libil2cpp.so", dump_global_metadata);
});