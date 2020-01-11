import * as React from 'react';
import { Button, ButtonType } from 'office-ui-fabric-react';
import Header from './Header';
import HeroList, { HeroListItem } from './HeroList';
import Progress from './Progress';
import { request } from 'graphql-request';
import RangeInput from "./RangeInput";

export interface AppProps {
  title: string;
  isOfficeInitialized: boolean;
}

export interface AppState {
  listItems: HeroListItem[];
}

export default class App extends React.Component<AppProps, AppState> {
  constructor(props, context) {
    super(props, context);
    this.state = {
      listItems: []
    };
  }

  componentDidMount() {
    this.setState({
      listItems: [ /*
        {
          icon: 'Ribbon',
          primaryText: 'Achieve more with Office integration'
        },
        {
          icon: 'Unlock',
          primaryText: 'Unlock features and functionality'
        },
        {
          icon: 'Design',
          primaryText: 'Create and visualize like a pro'
        }*/
      ]
    });
  }

  rootClick = async() => {
    try{
      await Excel.run(async context => {
        /**
         * Insert your Excel code here
         */
        const range = context.workbook.getSelectedRange();

        // Read the range address
        range.load("address");
        range.load("values");

        await context.sync();

        try {
          var rInput = new RangeInput(range.values);
          
          var names = rInput.getByHeader("Name");
          if (names != undefined) {
            var cur = range.values;
            cur[1][0] = names.join(',');
            console.log(`${names.join(',')}`);
            range.values = cur;
          }
          else {
            console.error("Name header not found!")
          }
              
        }
        catch (error) {
          range.values[0][0] = JSON.stringify(error.message, undefined, 2);
          console.error(error);
        }
        
        await context.sync();
      })
    } 
    catch (error) {console.error(error);}
  }
  
  click = async () => {
    try {
      await Excel.run(async context => {
        /**
         * Insert your Excel code here
         */
        const range = context.workbook.getSelectedRange();

        // Read the range address
        range.load("address");

        // query graphQL

        const query = `{
          statsQuery { numberOfRoots }
        }`;

        // process.env.NODE_TLS_REJECT_UNAUTHORIZED = "0";
        try{
          const data = await request('https://localhost:5001', query);
          range.values = data.statsQuery.numberOfRoots; //JSON.stringify(data, undefined, 2);
        }
        catch(error){
          range.values[0][0] = JSON.stringify(error.message, undefined, 2); 
        }
        
        // Update the fill color
        
        // range.format.fill.color = "yellow";

        await context.sync();
        console.log(`The range address was ${range.address}.`);
      });
    } catch (error) {
      console.error(error);
    }
  }

  render() {
    const {
      title,
      isOfficeInitialized,
    } = this.props;

    if (!isOfficeInitialized) {
      return (
        <Progress
          title={title}
          logo='assets/logo-filled.png'
          message='Please sideload your addin to see app body.'
        />
      );
    }

    return (
      <div className='ms-welcome'>
        <Header logo='assets/logo-filled.png' title={this.props.title} message='Welcome' />
        <HeroList message='' items={this.state.listItems}>
          <p className='ms-font-l'>Modify the source files, then click <b>Run</b>.</p>
          <Button className='ms-welcome__action' buttonType={ButtonType.hero} iconProps={{ iconName: 'ChevronRight' }} onClick={this.click}>Run</Button>
          <Button className='ms-root__action' buttonType={ButtonType.hero} iconProps={{ iconName: 'ChevronRight' }} onClick={this.rootClick}>Create root</Button>
        </HeroList>
      </div>
    );
  }
}
