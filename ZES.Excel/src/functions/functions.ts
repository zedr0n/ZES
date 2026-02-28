import {Query, SingleQuery} from './queries';
import {v4 as uuidv4} from 'uuid';
import {getId} from "@fluentui/react";

class RootInfo {
  rootId : string;
  createdAt: string;
  updatedAt: string;
  numberOfUpdates: number;
  
  constructor(rootId, createdAt, updatedAt, numberOfUpdates) {
    this.rootId = rootId;
    this.createdAt = new Date(createdAt).toISOString();
    this.updatedAt = new Date(updatedAt).toISOString()
    this.numberOfUpdates = Number.parseInt(numberOfUpdates)
  }
}

function getIdOrError(id : string, parseFn: ( data : any ) => string ) : (data : any) => string {
  return data => {
    let res = parseFn(data);
    if (res == "true" || res == "false")
      return id;
    else
      return res;
  };
}

/**
 * @customfunction
 */
export async function guid() : Promise<any> {

  let guid = uuidv4();
  return guid;
}

/**
 * @customfunction
 * @param invocation Custom function handler
 */
export function activeBranch(invocation : CustomFunctions.StreamingInvocation<string>) : void {
  const query = `query { activeBranch }`;

  Query(query, data => data.activeBranch.toString(), invocation);
}

/**
 * @customfunction
 * @param {string} id root id
 * @param {string} guid command guid
 */
export async function createRoot(id : string, guid: string) : Promise<any> {
  const query = `mutation { createRoot(name: \"${id}\", guid: \"${guid}\") }`

  let result = await SingleQuery(query, getIdOrError(id, data => data.createRoot.toString()))
  window.console.log(result)
  return result
}

/**
 * @customfunction
 * @param {string} id root id
 * @param {string} guid command guid
 */
export async function updateRoot(id : string, guid: string) : Promise<any> {
  const query = `mutation { updateRoot(name: \"${id}\", guid: \"${guid}\") }`

  let result = await SingleQuery(query, getIdOrError(id, data => data.updateRoot.toString()))
  window.console.log(result)
  return result
}


/**
 * @customfunction
 * @param {string} id root id
 */
export async function getOrAddRoot(id : string) : Promise<any> {
  let info = await rootInfo(id);
  if (info != "")
    return info
    
  const query = `mutation { createRoot(name: \"${id}\") }`
  
  let result = await SingleQuery(query, getIdOrError(id,data => data.createRoot.toString()))
  window.console.log(result)
  return result
}

/**
 * @customfunction
 * @param {string} id root id
 */
export async function rootInfo(id : string) : Promise<any> {
  const query = `
  query { rootInfoQuery(id: \"${id}\") {
    rootId
    createdAt
    updatedAt
    numberOfUpdates
  }}
  `
  
  let result = await SingleQuery(query, data => data.rootInfoQuery)
  if(!result.rootId)
    return ""
  
  const myEntity: Excel.EntityCellValue = {
    type: Excel.CellValueType.entity,
    text: id,
    properties : {
      "rootId" : {
        type: Excel.CellValueType.string,
        basicValue : result.rootId || ""
      },
      "createdAt" : {
        type : Excel.CellValueType.string,
        basicValue : new Date(Number.parseInt(result.createdAt)/10000).toISOString() || ""
      },
      "updatedAt" : {
        type: Excel.CellValueType.string,
        basicValue : new Date(Number.parseInt(result.updatedAt)/10000).toISOString() || ""
      },
      numberOfUpdates : {
        type: Excel.CellValueType.double,
        basicValue : result.numberOfUpdates 
      }
    },
  }
  return myEntity;
}